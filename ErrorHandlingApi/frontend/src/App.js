import React, { useState, useEffect, useRef } from 'react';
import axios from 'axios';
import { PieChart, Pie, Cell, Tooltip, Legend } from 'recharts';

const COLORS = ['#003366', '#3366CC', '#6699FF', '#000000', '#CCCCCC'];

const BASE_URL = 'http://localhost:5262/api/loganalysis/';

const analysisOptions = [
  { key: 'count-log-types', label: 'Count Log Types' },
  { key: 'count-log-types-per-hex', label: 'Count Log Types Per Hex' },
  { key: 'calculate-time-differences', label: 'Time Differences Per Hex' },
  { key: 'extract-topics', label: 'Extract Topics After Hex' },
  { key: 'topic-wise-time-diff', label: 'Topic-wise Time Differences' },
  { key: 'error-to-hex-ratio', label: 'Error to Hex ID Ratio' },
];

const styles = {
  container: {
    maxWidth: 1200,
    margin: "0 auto",
    padding: 32,
    fontFamily: "'Inter', sans-serif",
    backgroundColor: "#fff",
    color: "#6b7280",
    minHeight: "100vh",
  },
  header: {
    marginBottom: 32,
    borderBottom: "1px solid #e5e7eb",
  },
  title: {
    fontSize: 48,
    fontWeight: 800,
    color: "#111827",
    textAlign: "center",
    paddingBottom: 16,
  },
  main: {},
  card: {
    marginBottom: 24,
    backgroundColor: "#f9fafb",
    padding: 20,
    borderRadius: 12,
    boxShadow: "0 2px 8px rgb(0 0 0 / 0.1)",
  },
  heading: {
    fontSize: 24,
    fontWeight: 700,
    marginBottom: 12,
    color: "#111827",
  },
  select: {
    width: "100%",
    padding: 12,
    fontSize: 16,
    borderRadius: 12,
    border: "1px solid #d1d5db",
    cursor: "pointer",
  },
  text: {
    color: '#374151',
  },
  popupOverlay: {
    position: 'fixed',
    top: 0,
    left: 0,
    width: '100vw',
    height: '100vh',
    backgroundColor: 'rgba(0,0,0,0.5)',
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    zIndex: 10000,
  },
  popupContent: {
    backgroundColor: 'white',
    padding: 24,
    borderRadius: 8,
    maxWidth: 400,
    textAlign: 'center',
    boxShadow: '0 2px 10px rgba(0,0,0,0.3)',
  },
  popupButton: {
    marginTop: 16,
    padding: '8px 16px',
    fontSize: 16,
    borderRadius: 6,
    border: 'none',
    cursor: 'pointer',
  },
  popupButtonClose: {
    backgroundColor: '#ccc',
    marginRight: 8,
  },
  popupButtonOk: {
    backgroundColor: '#007bff',
    color: 'white',
  },
};

export default function App() {
  const [files, setFiles] = useState([]);
  const [selectedFile, setSelectedFile] = useState('');
  const [selectedOption, setSelectedOption] = useState(analysisOptions[0].key);
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [selectedHex, setSelectedHex] = useState(null);
  const [toastMessage, setToastMessage] = useState('');
  const [showToast, setShowToast] = useState(false);
  const [showWarningPopup, setShowWarningPopup] = useState(false);

  const latestDataRef = useRef(null);

  useEffect(() => {
    async function loadFiles() {
      setError(null);
      try {
        const res = await axios.get(`${BASE_URL}files`);
        setFiles(res.data);
      } catch {
        setError('Failed to load files from server.');
      }
    }
    loadFiles();
  }, []);

  useEffect(() => {
    if (!selectedFile || !selectedOption) {
      setData(null);
      setSelectedHex(null);
      latestDataRef.current = null;
      setShowWarningPopup(false);
      return;
    }

    let isMounted = true;

    async function fetchData() {
      try {
        const res = await axios.get(`${BASE_URL}${selectedOption}`, {
          params: { filePath: selectedFile },
        });
        const newDataString = JSON.stringify(res.data);

        if (latestDataRef.current !== newDataString && isMounted) {
          setData(res.data);
          latestDataRef.current = newDataString;

          if (
            (selectedOption === 'count-log-types-per-hex' ||
             selectedOption === 'extract-topics') &&
            res.data
          ) {
            const hexes = Object.keys(res.data).filter(h => h && !/^0+$/.test(h));
            setSelectedHex(hexes.length ? hexes[0] : null);
          }

          if (selectedOption === 'error-to-hex-ratio') {
            const { ratios, average } = res.data;
            const totalErrors = ratios ? Object.values(ratios).reduce((a, b) => a + b, 0) : 0;

            // Show warning popup if totalErrors is >= 90% of average threshold
            if (totalErrors >= average * 0.9 && totalErrors < average) {
              setShowWarningPopup(true);
            } else {
              setShowWarningPopup(false);
            }

            // Existing immediate email sending if totalErrors > average
            if (totalErrors > average) {
              await axios.post(`${BASE_URL}send-email`, {
                subject: `High Errors in ${selectedFile}`,
                body: `File ${selectedFile} has error count ${totalErrors} which exceeds average ${average}.`
              });
              setToastMessage(`Alert sent for ${selectedFile}`);
              setShowToast(true);
              setTimeout(() => setShowToast(false), 3000);
            }
          }
        }

        setLoading(false);
        setError(null);
      } catch {
        if (isMounted) {
          setError('Failed to fetch analysis data.');
          setLoading(false);
        }
      }
    }

    setLoading(true);
    fetchData();

    return () => {
      isMounted = false;
      setLoading(false);
    };
  }, [selectedFile, selectedOption]);

  const closeWarningPopup = () => {
    setShowWarningPopup(false);
  };

  const renderWarningPopup = () => {
    if (!showWarningPopup) return null;
    return (
      <div style={styles.popupOverlay} role="alertdialog" aria-modal="true" aria-labelledby="warning-title" aria-describedby="warning-desc">
        <div style={styles.popupContent}>
          <h2 id="warning-title">Warning: Approaching Error Limit</h2>
          <p id="warning-desc">
            The error count for the selected file is approaching the threshold. Please monitor closely.
          </p>
          <button
            style={{ ...styles.popupButton, ...styles.popupButtonOk }}
            onClick={closeWarningPopup}
            aria-label="Close warning popup"
          >
            OK
          </button>
        </div>
      </div>
    );
  };

  const renderFileSelector = (
    <section style={styles.card} aria-label="Select file">
      <h2 style={styles.heading}>Select Log File</h2>
      <select
        style={styles.select}
        value={selectedFile}
        onChange={(e) => setSelectedFile(e.target.value)}
        aria-label="Select a log file"
      >
        <option value="" disabled>
          -- Select a file --
        </option>
        {files.map((f) => (
          <option key={f} value={f}>
            {f}
          </option>
        ))}
      </select>
    </section>
  );

  const renderOptionSelector = (
    <section style={styles.card} aria-label="Select analysis option">
      <h2 style={styles.heading}>Select Analysis Option</h2>
      <select
        style={styles.select}
        value={selectedOption}
        onChange={(e) => setSelectedOption(e.target.value)}
        aria-label="Select an analysis option"
      >
        {analysisOptions.map(({ key, label }) => (
          <option key={key} value={key}>
            {label}
          </option>
        ))}
      </select>
    </section>
  );

  const renderContent = () => {
    if (loading) return <p style={{ textAlign: 'center', padding: 20 }}>Loading...</p>;
    if (error) return <p style={{ color: 'red', textAlign: 'center', padding: 20 }}>{error}</p>;

    switch (selectedOption) {
      case "count-log-types":
        return renderLogTypeCounts();
      case "count-log-types-per-hex":
        return renderLogCountsPerHex();
      case "calculate-time-differences":
        return renderTimeDifferences();
      case "extract-topics":
        return renderTopicsAfterHex();
      case "topic-wise-time-diff":
        return renderTopicWiseTimeDiff();
      case "error-to-hex-ratio":
        return renderErrorToHexRatio();
      default:
        return null;
    }
  };

  const renderLogTypeCounts = () => {
    if (!data) return null;
    const chartData = Object.entries(data).map(([name, value]) => ({
      name,
      value: Number(value) || 0,
    }));
    return (
      <section style={styles.card} aria-label="Log Type Counts">
        <h2 style={styles.heading}>Log Type Counts</h2>
        <PieChart width={400} height={400}>
          <Pie
            data={chartData}
            dataKey="value"
            nameKey="name"
            outerRadius={150}
            cx="50%"
            cy="50%"
            label
          >
            {chartData.map((entry, index) => (
              <Cell key={index} fill={COLORS[index % COLORS.length]} />
            ))}
          </Pie>
          <Tooltip />
          <Legend />
        </PieChart>
      </section>
    );
  };

  const renderLogCountsPerHex = () => {
    if (!data) return <p style={styles.text}>No analysis data available.</p>;
    if (!selectedHex || !data[selectedHex])
      return <p style={styles.text}>No data available for selected Hex ID.</p>;

    const hexData = Object.entries(data[selectedHex]).map(([name, value]) => ({
      name,
      value: Number(value) || 0,
    }));
    const hexKeys = Object.keys(data).filter((h) => h && !/^0+$/.test(h));

    return (
      <section style={styles.card} aria-label="Log Counts Per Hex">
        <h2 style={styles.heading}>Log Counts for Hex ID: {selectedHex}</h2>
        <HexDropdown
          hexKeys={hexKeys}
          selected={selectedHex}
          onChange={setSelectedHex}
        />
        <PieChart width={400} height={400} aria-label="Log Counts Per Hex Chart">
          <Pie
            data={hexData}
            dataKey="value"
            nameKey="name"
            outerRadius={150}
            cx="50%"
            cy="50%"
            label
          >
            {hexData.map((entry, index) => (
              <Cell key={index} fill={COLORS[index % COLORS.length]} />
            ))}
          </Pie>
          <Tooltip />
          <Legend />
        </PieChart>
      </section>
    );
  };

  const renderTimeDifferences = () => {
    if (!data) return null;
    const sortedEntries = Object.entries(data).sort((a, b) => b[1] - a[1]);
    return (
      <section style={styles.card} aria-label="Time Differences Per Hex">
        <h2 style={styles.heading}>Time Differences per Hex</h2>
        <ul>
          {sortedEntries.map(([hex, seconds]) => (
            <li key={hex}>
              <strong>{hex}:</strong> {seconds.toFixed(2)} seconds
            </li>
          ))}
        </ul>
      </section>
    );
  };

  const renderTopicsAfterHex = () => {
    if (!data) return null;
    const hexKeys = Object.keys(data).filter((h) => h && !/^0+$/.test(h));
    if (!selectedHex && hexKeys.length > 0) setSelectedHex(hexKeys[0]);
    return (
      <section style={styles.card} aria-label="Extract Topics After Hex">
        <h2 style={styles.heading}>Extract Topics After Hex</h2>
        <HexDropdown
          hexKeys={hexKeys}
          selected={selectedHex}
          onChange={setSelectedHex}
        />
        {selectedHex && data[selectedHex] && (
          <ul>
            {(Array.isArray(data[selectedHex])
              ? data[selectedHex]
              : Object.keys(data[selectedHex])
            ).map((topic) => (
              <li key={topic}>{topic}</li>
            ))}
          </ul>
        )}
      </section>
    );
  };

  const renderTopicWiseTimeDiff = () => {
    if (!data) return null;
    const validTopics = Object.entries(data).filter(
      ([topic, vals]) =>
        vals && Object.keys(vals).length > 0 && vals.Average !== 0
    );
    if (validTopics.length === 0) {
      return (
        <p style={styles.text}>No valid topic-wise time difference data found.</p>
      );
    }
    return (
      <section style={styles.card} aria-label="Topic-wise Time Differences">
        <h2 style={styles.heading}>Topic-wise Time Differences</h2>
        {validTopics.map(([topic, vals]) => (
          <div key={topic} style={{ marginBottom: 20 }}>
            <h3 style={{ color: COLORS[1] }}>{topic}</h3>
            <ul>
              {Object.entries(vals).map(([hexId, val]) =>
                hexId !== "Average" ? (
                  <li key={hexId}>
                    <strong>{hexId}:</strong> {val.toFixed(2)} seconds
                  </li>
                ) : null
              )}
            </ul>
            <p>
              <strong>Average:</strong> {vals.Average.toFixed(2)} seconds
            </p>
          </div>
        ))}
      </section>
    );
  };

  const renderErrorToHexRatio = () => {
    if (!data) return null;

    const { ratios, average } = data;

    if (!ratios || Object.keys(ratios).length === 0) {
      return <p style={styles.text}>No error ratio data available.</p>;
    }

    const sortedRatios = Object.entries(ratios)
      .filter(([hexId]) => hexId && !/^0+$/.test(hexId)) 
      .sort(([, ratioA], [, ratioB]) => ratioB - ratioA); 

    return (
      <section style={styles.card} aria-label="Error to Hex ID Ratio per Hex">
        <h2 style={styles.heading}>Error to Hex ID Ratio per Hex</h2>
        <ul>
          {sortedRatios.map(([hexId, ratio]) => (
            <li key={hexId}>
              <strong>{hexId}:</strong> {(ratio * 100).toFixed(2)}% errors
            </li>
          ))}
        </ul>
        <p>
          <strong>Average Error Ratio:</strong> {(average * 100).toFixed(2)}%
        </p>
      </section>
    );
  };

  function HexDropdown({ hexKeys, selected, onChange }) {
    if (!hexKeys || hexKeys.length === 0)
      return <p style={styles.text}>No valid Hex IDs found.</p>;
    return (
      <select
        value={selected || ''}
        onChange={(e) => onChange(e.target.value)}
        style={styles.select}
        aria-label="Select Hex ID"
      >
        {hexKeys.map((hex) => (
          <option key={hex} value={hex}>
            {hex}
          </option>
        ))}
      </select>
    );
  }

  return (
    <div style={styles.container}>
      <header style={styles.header}>
        <h1 style={styles.title}>Log Analysis Dashboard</h1>
      </header>

      <main style={styles.main}>
        {renderFileSelector}
        {renderOptionSelector}
        {selectedFile && selectedOption && renderContent()}
      </main>

      {showToast && (
        <div style={{
          position: 'fixed',
          bottom: '20px',
          right: '20px',
          backgroundColor: '#003366',
          color: 'white',
          padding: '12px 20px',
          borderRadius: '8px',
          boxShadow: '0 2px 8px rgba(0,0,0,0.3)',
          zIndex: 1000,
          transition: 'opacity 0.3s ease',
        }}>
          {toastMessage}
        </div>
      )}

      {renderWarningPopup()}
    </div>
  );
}
