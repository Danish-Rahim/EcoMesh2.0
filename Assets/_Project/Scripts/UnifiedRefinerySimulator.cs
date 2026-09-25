using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UnifiedRefinerySimulator : MonoBehaviour
{
    // ==========================================
    // REFINERY ENGINE VARIABLES
    // ==========================================
    [System.Serializable]
    public struct SimulationHistoryRecord
    {
        public string timestamp;
        public bool isSuccess;
        public float efficiency;
        public float dailyCost;
        public float pressureDrop;
        public float outletPpm;
        public string grade;

        public float inputGasVolume;
        public float inputH2S;
        public float inputTemp;
        public string inputMaterial;
        public float inputBedDepth;
        public string inputOpeningSize;
        public float runDuration;
        public float serviceLife;
    }

    [System.Serializable]
    private class HistoryWrapper
    {
        public List<SimulationHistoryRecord> historyList;
    }

    private List<SimulationHistoryRecord> simulationHistoryLog = new List<SimulationHistoryRecord>();
    private const string HISTORY_SAVE_KEY = "Refinery_Simulation_History_V1";

    static private float runtimeCountdownClockStatic = 11.0f;
    private float runtimeCountdownClock = runtimeCountdownClockStatic;
    private float meshSaturationAccumulator = 0.0f;

    public enum SimulationState { STANDBY, RUNNING, CONCLUDED }
    private SimulationState currentRunState = SimulationState.STANDBY;

    // ==========================================
    // INSPECTOR ASSIGNMENTS (UI & 3D Objects)
    // ==========================================
    [Header("Data Profile Pool")]
    public MeshProfile[] materialProfiles;

    [Header("Hardware Configuration & Sprint Gates")]
    public CanvasGroup panel1HardwareCanvasGroup;
    public TMP_Dropdown meshMaterialDropdown;
    public TMP_Dropdown discreteBedDepthDropdown;
    public TMP_Dropdown meshOpeningSizeDropdown;
    public Button btnGenerateModel;

    [Header("Hardware Meshes")]
    public GameObject[] catalystMeshes;
    public Renderer reactorMainBodyRenderer;

    [Header("Stream Controls (Main UI)")]
    public Slider gasVolumeSlider;
    public Slider h2sSlider;
    public Slider temperatureSlider;
    public Slider inletPressureSlider;


    [Header("Fullscreen Mirrored Controls")]
    public Slider fullscreenGasVolumeSlider;
    public Slider fullscreenH2SSlider;
    public Slider fullscreenTemperatureSlider;
    public Slider fullscreenInletPressureSlider;
    public Button fullscreenMainRunButton;
    private TextMeshProUGUI fullscreenRunButtonText;

    [Header("User Estimation Inputs")]
    public Slider expectedEfficiencySlider;
    public Slider estimatedCostSlider;

    [Header("Persistent Viewport Telemetry")]
    public TextMeshProUGUI rightSideEfficiencyText;
    public TextMeshProUGUI rightSideCostText;
    public Button mainRunButton;
    private TextMeshProUGUI runButtonText;

    [Header("Detailed Telemetry Readouts")]
    public TextMeshProUGUI detailedPressureDropText;
    public TextMeshProUGUI detailedOutletPpmText;
    public TextMeshProUGUI detailedServiceLifeText;
    public TextMeshProUGUI detailedComplianceStatusText;

    [Header("Live Alarm & Warning System")]
    public TextMeshProUGUI alarmHeaderStatusText;
    public TextMeshProUGUI alarmLogText;
    private List<string> alarmLogs = new List<string>();
    private float alarmUpdateTimer = 0f;
    private bool wasInAlarmState = false;

    [Header("Performance Summary UI (Primary View)")]
    public TextMeshProUGUI perfOutletH2SText;
    public TextMeshProUGUI perfTempText;
    public TextMeshProUGUI perfPressureText;
    public TextMeshProUGUI perfEfficiencyText;
    public TextMeshProUGUI perfServiceLifeText;

    [Header("Performance Summary UI (Secondary View)")]
    public TextMeshProUGUI altPerfOutletH2SText;
    public TextMeshProUGUI altPerfTempText;
    public TextMeshProUGUI altPerfPressureText;
    public TextMeshProUGUI altPerfEfficiencyText;
    public TextMeshProUGUI altPerfServiceLifeText;

    [Header("Equipment Overview UI")]
    public TextMeshProUGUI equipmentModelIdText;
    public TextMeshProUGUI equipmentMaterialText;
    public TextMeshProUGUI equipmentBedDepthText;
    public TextMeshProUGUI equipmentMeshSizeText;

    [Header("Graph Header Readouts")]
    public TextMeshProUGUI graphHeaderEfficiencyText;
    public TextMeshProUGUI graphHeaderExpectedEfficiencyText;
    public TextMeshProUGUI graphHeaderPressureText;
    public TextMeshProUGUI graphHeaderSafePressureText;
    public TextMeshProUGUI graphHeaderOutletText;
    public TextMeshProUGUI graphHeaderSafeOutletText;
    public TextMeshProUGUI graphHeaderTempText;
    public TextMeshProUGUI graphHeaderSafeTempText;

    private float perfSummaryTimer = 1.0f;

    [Header("User Audit & Operational Log System")]
    public GameObject userLogsModal;              // Assign your log modal panel here
    public TextMeshProUGUI logContentText;        // Assign the scrollable TextMeshPro text here
    public TextMeshProUGUI logSummaryHeader;      // Assign modal status/header text here
    public Button btnShowUserLogs;                // Assign your UI button here
    public Button btnCloseUserLogs;               // Assign modal close button here
    public Button btnClearUserLogs;               // (Optional) purge button

    private List<string> operationalAuditLog = new List<string>();
    private const int MaxLogEntries = 60;
    private const string AUDIT_LOG_PREF_KEY = "Refinery_Audit_Logs_V1";


    [Header("Historical Reports & Graphing")]
    public TextMeshProUGUI[] historyRowTexts;
    public RectTransform graphBoundingBox;
    public RectTransform graphTrackingNode;

    // Separated Graphs for individual panels
    public LiveECGGraph efficiencyGraph;
    public LiveECGGraph pressureGraph;
    public LiveECGGraph outletH2SGraph;
    public LiveECGGraph temperatureGraph;

    [Header("Evaluation Popup Windows")]
    public GameObject evaluationOverlayPanel;
    public TextMeshProUGUI evaluationTitleText;
    public TextMeshProUGUI evaluationReportText;
    public Button restartRunButton;
    public Button closeEvaluationPopupButton;

    [Header("System Application Controls")]
    public Button quitApplicationButton;
    public Button maximizeViewportButton;
    public Button closeFullscreenButton;
    public GameObject fullscreenOverlayPanel;

    [Header("Particle Process Simulation")]
    public ParticleSystem inletParticles;
    public ParticleSystem outletParticles;

    [Header("Camera & Zoom Settings")]
    public Camera studioCamera;
    public float zoomSpeed = 2f;
    public float minZoom = 3f;
    public float maxZoom = 15f;

    [Header("Color Configurations")]
    public Color baseReactorColor = new Color(0.48f, 0.48f, 0.48f, 1f);
    public Color heatedReactorColor = new Color(0.85f, 0.25f, 0.15f, 1f);

    // ==========================================
    // BACKEND MATH CACHE
    // ==========================================
    private float cachedEfficiency = 0f;
    private float cachedDailyCost = 0f;
    private float cachedPressureDrop = 0f;
    private float cachedOutletPpm = 0f;
    private float cachedServiceLife = 0f;
    private bool isStartIteration = false;

    private int cachedMaterialIndex = 0;
    private float cachedBedDepthL = 1.2f;
    private int cachedOpeningSizeIndex = 0;

    private Material[] instancedMaterials;
    private Material reactorMaterial;

    private void Start()
    {
        try
        {
            if (mainRunButton != null)
            {
                runButtonText = mainRunButton.GetComponentInChildren<TextMeshProUGUI>();
                mainRunButton.onClick.AddListener(OnMainRunButtonClicked);
            }

            if (fullscreenMainRunButton != null)
            {
                fullscreenRunButtonText = fullscreenMainRunButton.GetComponentInChildren<TextMeshProUGUI>();
                fullscreenMainRunButton.onClick.AddListener(OnMainRunButtonClicked);
            }

            if (btnGenerateModel != null) btnGenerateModel.onClick.AddListener(OnGenerateHardwareModelConfirmed);
            if (restartRunButton != null) restartRunButton.onClick.AddListener(ResetSimulationToStandby);
            if (btnShowUserLogs != null) btnShowUserLogs.onClick.AddListener(ToggleUserLogsModal);
            if (btnCloseUserLogs != null) btnCloseUserLogs.onClick.AddListener(() => SetUserLogsModalActive(false));
            if (btnClearUserLogs != null) btnClearUserLogs.onClick.AddListener(ClearUserLogs);

            LoadAuditLogsFromStorage();
            if (operationalAuditLog.Count == 0)
            {
                RecordLogEntry("SYSTEM_INIT", "Training Simulator initialized. Sensor polling active.", "NORMAL");
            }
            
            if (closeEvaluationPopupButton != null) closeEvaluationPopupButton.onClick.AddListener(CloseEvaluationPopup);
            if (quitApplicationButton != null) quitApplicationButton.onClick.AddListener(QuitRefinerySimulator);
            if (maximizeViewportButton != null) maximizeViewportButton.onClick.AddListener(() => SetFullscreenOverlayActive(true));
            if (closeFullscreenButton != null) closeFullscreenButton.onClick.AddListener(() => SetFullscreenOverlayActive(false));

            SyncSliders(gasVolumeSlider, fullscreenGasVolumeSlider);
            SyncSliders(h2sSlider, fullscreenH2SSlider);
            SyncSliders(temperatureSlider, fullscreenTemperatureSlider);
            SyncSliders(inletPressureSlider, fullscreenInletPressureSlider);

            if (catalystMeshes != null)
            {
                instancedMaterials = new Material[catalystMeshes.Length];
                for (int i = 0; i < catalystMeshes.Length; i++)
                {
                    if (catalystMeshes[i] != null)
                    {
                        Renderer r = catalystMeshes[i].GetComponent<Renderer>();
                        if (r != null) instancedMaterials[i] = r.material;
                    }
                }
            }

            if (reactorMainBodyRenderer != null) reactorMaterial = reactorMainBodyRenderer.material;
            isStartIteration = true;
            ClearParticles();
            LoadSimulationHistoryFromStorage();

            // --- ADD THESE LINES HERE ---
            string selectedMaterial = meshMaterialDropdown != null ? meshMaterialDropdown.options[meshMaterialDropdown.value].text : "Unknown";
            string selectedMeshSize = meshOpeningSizeDropdown != null ? meshOpeningSizeDropdown.options[meshOpeningSizeDropdown.value].text : "Unknown";

            if (equipmentMaterialText != null) 
                equipmentMaterialText.text = $"Material: {selectedMaterial}";

            if (equipmentBedDepthText != null) 
                equipmentBedDepthText.text = $"Bed depth: {cachedBedDepthL:F2} m";

            if (equipmentMeshSizeText != null) 
                equipmentMeshSizeText.text = $"Mesh size: {selectedMeshSize}";
            // ----------------------------


            ResetSimulationToStandby();
            UpdateHistoryLogDisplayUI();
            UpdateUnifiedMeshAppearance();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Start] Error: {e.Message}");
        }
    }

    private void Update()
    {
        try
        {
            EvaluateSystemPhysics();

            if (currentRunState == SimulationState.RUNNING)
            {
                ProcessSimulationCountdown();
                ProcessLiveAlarms();
                ProcessPerformanceSummary();
            }

            if (fullscreenOverlayPanel != null && fullscreenOverlayPanel.activeSelf)
            {
                HandleFullscreen3DClicking();
            }

            HandleZoom();
            CheckHistoryLogClicks();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Update] Error: {e.Message}");
        }
    }

    // ==========================================
    // HISTORY LOGIC & PERSISTENCE
    // ==========================================
    private void LoadSimulationHistoryFromStorage()
    {
        try
        {
            if (PlayerPrefs.HasKey(HISTORY_SAVE_KEY))
            {
                string json = PlayerPrefs.GetString(HISTORY_SAVE_KEY);
                HistoryWrapper wrapper = JsonUtility.FromJson<HistoryWrapper>(json);
                if (wrapper != null && wrapper.historyList != null)
                {
                    simulationHistoryLog = wrapper.historyList;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LoadHistory] Error: {e.Message}");
        }
    }

    private void SaveSimulationHistoryToStorage()
    {
        try
        {
            HistoryWrapper wrapper = new HistoryWrapper { historyList = this.simulationHistoryLog };
            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString(HISTORY_SAVE_KEY, json);
            PlayerPrefs.Save();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveHistory] Error: {e.Message}");
        }
    }

    private void CheckHistoryLogClicks()
    {
        try
        {
            if (Input.GetMouseButtonDown(0) && historyRowTexts != null)
            {
                for (int i = 0; i < historyRowTexts.Length; i++)
                {
                    TextMeshProUGUI rowText = historyRowTexts[i];
                    if (rowText != null && rowText.gameObject.activeInHierarchy)
                    {
                        int linkIndex = TMP_TextUtilities.FindIntersectingLink(rowText, Input.mousePosition, null);
                        if (linkIndex != -1)
                        {
                            TMP_LinkInfo linkInfo = rowText.textInfo.linkInfo[linkIndex];
                            if (int.TryParse(linkInfo.GetLinkID(), out int historyIndex))
                            {
                                OpenHistoryDetailPopup(historyIndex);
                                break;
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CheckHistoryClicks] Error: {e.Message}");
        }
    }

    private void OpenHistoryDetailPopup(int index)
    {
        try
        {
            if (index < 0 || index >= simulationHistoryLog.Count) return;

            SimulationHistoryRecord record = simulationHistoryLog[index];

            if (evaluationOverlayPanel != null) evaluationOverlayPanel.transform.SetAsLastSibling();

            string statusText = record.isSuccess ? "<color=green>SUCCESS</color>" : "<color=red>FAILED</color>";
            string popupTitle = $"<color=white>HISTORICAL RUN: {record.timestamp}</color>";

            string popupMessage = $"<b>RUN STATUS:</b> {statusText}\n\n" +
                                  $"<b>OPERATOR SELECTIONS:</b>\n" +
                                  $"Inlet Gas Flow: {record.inputGasVolume:F0} Nm3/h\n" +
                                  $"H2S Concentration: {record.inputH2S:F0} ppm\n" +
                                  $"Temperature: {record.inputTemp:F1} °C\n" +
                                  $"Hardware Material: {record.inputMaterial}\n" +
                                  $"Bed Depth: {record.inputBedDepth:F2} m\n" +
                                  $"Opening Size: {record.inputOpeningSize}\n\n" +
                                  $"<b>SIMULATION RESULTS ({record.runDuration:F0}s shift):</b>\n" +
                                  $"H2S Removal Efficiency: {record.efficiency:F2}%\n" +
                                  $"Operational Cost: €{record.dailyCost:F2}/day\n" +
                                  $"Pressure Drop: {record.pressureDrop:F2} kPa\n" +
                                  $"Outlet Toxic Exhaust: {record.outletPpm:F2} ppm\n" +
                                  $"Est. Material Service Life: {record.serviceLife:F0} Days";

            if (evaluationTitleText != null) evaluationTitleText.text = popupTitle;
            if (evaluationReportText != null) evaluationReportText.text = popupMessage;

            if (restartRunButton != null) restartRunButton.gameObject.SetActive(false);

            if (evaluationOverlayPanel != null) evaluationOverlayPanel.SetActive(true);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[OpenHistoryPopup] Error: {e.Message}");
        }
    }

    private void CloseEvaluationPopup()
    {
        if (evaluationOverlayPanel != null) evaluationOverlayPanel.SetActive(false);
        // Resets the simulation state, run buttons, graphs, and UI elements to standby
        ResetSimulationToStandby();
    }

    private void ArchiveRunToHistoryLog(bool wasSuccessful, string gradeEarned)
    {
        try
        {
            string matName = meshMaterialDropdown != null ? meshMaterialDropdown.options[meshMaterialDropdown.value].text : "Unknown";
            string openSize = meshOpeningSizeDropdown != null ? meshOpeningSizeDropdown.options[meshOpeningSizeDropdown.value].text : "Unknown";

            SimulationHistoryRecord record = new SimulationHistoryRecord
            {
                timestamp = System.DateTime.Now.ToString("dd/MM - HH:mm:ss"),
                isSuccess = wasSuccessful,
                efficiency = cachedEfficiency,
                dailyCost = cachedDailyCost,
                pressureDrop = cachedPressureDrop,
                outletPpm = cachedOutletPpm,
                grade = gradeEarned,

                inputGasVolume = gasVolumeSlider != null ? gasVolumeSlider.value : 0f,
                inputH2S = h2sSlider != null ? h2sSlider.value : 0f,
                inputTemp = temperatureSlider != null ? temperatureSlider.value : 0f,
                inputMaterial = matName,
                inputBedDepth = cachedBedDepthL,
                inputOpeningSize = openSize,
                runDuration = runtimeCountdownClockStatic,
                serviceLife = cachedServiceLife
            };

            simulationHistoryLog.Insert(0, record);
            while (simulationHistoryLog.Count > 10) simulationHistoryLog.RemoveAt(simulationHistoryLog.Count - 1);

            SaveSimulationHistoryToStorage();
            UpdateHistoryLogDisplayUI();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ArchiveRun] Error: {e.Message}");
        }
    }

    private void UpdateHistoryLogDisplayUI()
    {
        try
        {
            if (historyRowTexts == null || historyRowTexts.Length == 0) return;

            for (int i = 0; i < historyRowTexts.Length; i++)
            {
                if (historyRowTexts[i] == null) continue;

                if (i < simulationHistoryLog.Count)
                {
                    var run = simulationHistoryLog[i];
                    string statusColor = run.isSuccess ? "#00FF00" : "#FF4444";
                    string statusText = run.isSuccess ? "Successful" : "Unsuccessful";

                    historyRowTexts[i].text = $"<link=\"{i}\">[{run.timestamp}] - <color={statusColor}>{statusText}</color></link>";
                }
                else
                {
                    historyRowTexts[i].text = i == 0 ? "<i>No operational simulation runs logged yet.</i>" : "";
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UpdateHistoryUI] Error: {e.Message}");
        }
    }

    // ==========================================
    // LIVE ALARM & WARNING LOGIC
    // ==========================================
    private void ProcessLiveAlarms()
    {
        try
        {
            alarmUpdateTimer += Time.deltaTime;

            if (alarmUpdateTimer >= 1.0f)
            {
                alarmUpdateTimer = 0f;
                EvaluateCurrentAlarms();
            }
        }
        catch (System.Exception e)
        {
        }
    }

    private void EvaluateCurrentAlarms()
    {
        try
        {
            List<string> activeWarnings = new List<string>();

            if (cachedPressureDrop > 6.5f) activeWarnings.Add($"Pressure Critical ({cachedPressureDrop:F1} kPa)");
            if (cachedOutletPpm > 50.0f) activeWarnings.Add($"Toxic Leak ({cachedOutletPpm:F1} ppm)");
            if (cachedDailyCost > 3000f) activeWarnings.Add($"Budget Overflow (€{cachedDailyCost:F0})");

            string timeStamp = System.DateTime.Now.ToString("HH:mm:ss");
            bool isInAlarmState = activeWarnings.Count > 0;

            if (isInAlarmState)
            {
                string combinedWarnings = string.Join(" | ", activeWarnings);
                AddMessageToAlarmLog($"[{timeStamp}] <color=red>WARNING: {combinedWarnings}</color>");
                UpdateAlarmHeaderUI(true);

                if (isInAlarmState && !wasInAlarmState)
            {
                RecordLogEntry("ALARM_TRIGGER", $"Exceeded safety limits: {string.Join(" | ", activeWarnings)}", "WARNING");
            }
            }
            else if (wasInAlarmState)
            {
                AddMessageToAlarmLog($"[{timeStamp}] <color=green>STABILIZED: All metrics within safe parameters.</color>");
                UpdateAlarmHeaderUI(false);
            }

            wasInAlarmState = isInAlarmState;
        }
        catch (System.Exception e)
        {
        }
    }

    private void AddMessageToAlarmLog(string message)
    {
        try
        {
            alarmLogs.Insert(0, message);
            if (alarmLogs.Count > 3) alarmLogs.RemoveAt(alarmLogs.Count - 1);
            if (alarmLogText != null) alarmLogText.text = string.Join("\n", alarmLogs);
        }
        catch (System.Exception e)
        {
        }
    }

    private void UpdateAlarmHeaderUI(bool hasActiveAlarm)
    {
        try
        {
            if (alarmHeaderStatusText == null) return;
            alarmHeaderStatusText.text = hasActiveAlarm ? "<color=red>ACTIVE WARNINGS</color>" : "<color=green>NO ACTIVE ALARMS</color>";
        }
        catch (System.Exception e)
        {
        }
    }

    // ==========================================
    // REAL-TIME PERFORMANCE SUMMARY POLLING
    // ==========================================
    private void ProcessPerformanceSummary()
    {
        try
        {
            perfSummaryTimer += Time.deltaTime;

            if (perfSummaryTimer >= 1.0f)
            {
                perfSummaryTimer = 0f;

                float fluctuation = Random.Range(-0.008f, 0.008f);

                float liveEff = Mathf.Clamp(cachedEfficiency * (1f + fluctuation), 0f, 99.99f);
                float livePress = cachedPressureDrop * (1f + fluctuation);
                float liveOutlet = cachedOutletPpm * (1f + fluctuation);
                float liveTemp = (temperatureSlider != null ? temperatureSlider.value : 55f) + Random.Range(-0.3f, 0.3f);
                float liveLife = cachedServiceLife * (1f + fluctuation);
                float expectedEff = expectedEfficiencySlider != null ? expectedEfficiencySlider.value : 85f;

                string lineOutlet = $"Outlet H2S: {liveOutlet:F2} ppm";
                string lineTemp = $"Temperature: {liveTemp:F1} °C";
                string linePress = $"Pressure Drop: {livePress:F2} kPa";
                string lineEff = $"Efficiency: {liveEff:F1}%";
                string lineLife = $"Service Life: {Mathf.Max(0, liveLife):F1} Days";

                if (perfOutletH2SText != null) perfOutletH2SText.text = lineOutlet;
                if (perfTempText != null) perfTempText.text = lineTemp;
                if (perfPressureText != null) perfPressureText.text = linePress;
                if (perfEfficiencyText != null) perfEfficiencyText.text = lineEff;
                if (perfServiceLifeText != null) perfServiceLifeText.text = lineLife;

                if (altPerfOutletH2SText != null) altPerfOutletH2SText.text = lineOutlet;
                if (altPerfTempText != null) altPerfTempText.text = lineTemp;
                if (altPerfPressureText != null) altPerfPressureText.text = linePress;
                if (altPerfEfficiencyText != null) altPerfEfficiencyText.text = lineEff;
                if (altPerfServiceLifeText != null) altPerfServiceLifeText.text = lineLife;

                if (graphHeaderEfficiencyText != null) graphHeaderEfficiencyText.text = $"{liveEff:F1}%";
                if (graphHeaderExpectedEfficiencyText != null) graphHeaderExpectedEfficiencyText.text = $"Exp: {expectedEff:F1}%";

                if (graphHeaderPressureText != null) graphHeaderPressureText.text = $"{livePress:F3} kPa";
                if (graphHeaderSafePressureText != null) graphHeaderSafePressureText.text = "Safe: < 6.5 kPa";

                if (graphHeaderOutletText != null) graphHeaderOutletText.text = $"{liveOutlet:F2} ppm";
                if (graphHeaderSafeOutletText != null) graphHeaderSafeOutletText.text = "Safe: < 50.0 ppm";

                if (graphHeaderTempText != null) graphHeaderTempText.text = $"{liveTemp:F1} °C";
                if (graphHeaderSafeTempText != null) graphHeaderSafeTempText.text = "Std: 55.0 °C";
            }
        }
        catch (System.Exception e)
        {
        }
    }

    private void ResetPerformanceSummaryUI()
    {
        try
        {
            string defaultOutlet = "Outlet H2S: 0.00 ppm";
            string defaultTemp = "Temperature: 0.0 °C";
            string defaultPress = "Pressure Drop: 0.00 kPa";
            string defaultEff = "Efficiency: 0.0%";
            string defaultLife = "Service Life: 0.0 Days";

            if (perfOutletH2SText != null) perfOutletH2SText.text = defaultOutlet;
            if (perfTempText != null) perfTempText.text = defaultTemp;
            if (perfPressureText != null) perfPressureText.text = defaultPress;
            if (perfEfficiencyText != null) perfEfficiencyText.text = defaultEff;
            if (perfServiceLifeText != null) perfServiceLifeText.text = defaultLife;

            if (altPerfOutletH2SText != null) altPerfOutletH2SText.text = defaultOutlet;
            if (altPerfTempText != null) altPerfTempText.text = defaultTemp;
            if (altPerfPressureText != null) altPerfPressureText.text = defaultPress;
            if (altPerfEfficiencyText != null) altPerfEfficiencyText.text = defaultEff;
            if (altPerfServiceLifeText != null) altPerfServiceLifeText.text = defaultLife;

            if (graphHeaderEfficiencyText != null) graphHeaderEfficiencyText.text = "0.0%";
            if (graphHeaderExpectedEfficiencyText != null) graphHeaderExpectedEfficiencyText.text = "Exp: 0.0%";

            if (graphHeaderPressureText != null) graphHeaderPressureText.text = "0.000 kPa";
            if (graphHeaderSafePressureText != null) graphHeaderSafePressureText.text = "Safe: < 6.5 kPa";

            if (graphHeaderOutletText != null) graphHeaderOutletText.text = "0.00 ppm";
            if (graphHeaderSafeOutletText != null) graphHeaderSafeOutletText.text = "Safe: < 50.0 ppm";

            if (graphHeaderTempText != null) graphHeaderTempText.text = "0.0 °C";
            if (graphHeaderSafeTempText != null) graphHeaderSafeTempText.text = "Std: 25.0 °C";
        }
        catch (System.Exception e)
        {
        }
    }

    // ==========================================
    // UI SYNC & CORE SIMULATION LIFECYCLE
    // ==========================================
    private void SyncSliders(Slider mainSlider, Slider fullScreenSlider)
    {
        try
        {
            if (mainSlider == null || fullScreenSlider == null) return;
            fullScreenSlider.value = mainSlider.value;
            mainSlider.onValueChanged.AddListener((val) => { if (fullScreenSlider.value != val) fullScreenSlider.value = val; });
            fullScreenSlider.onValueChanged.AddListener((val) => { if (mainSlider.value != val) mainSlider.value = val; });
        }
        catch (System.Exception e)
        {
        }
    }

    private void OnGenerateHardwareModelConfirmed()
    {
        try
        {
            if (meshMaterialDropdown != null) cachedMaterialIndex = meshMaterialDropdown.value;
            if (meshOpeningSizeDropdown != null) cachedOpeningSizeIndex = meshOpeningSizeDropdown.value;

            if (discreteBedDepthDropdown != null)
            {
                switch (discreteBedDepthDropdown.value)
                {
                    case 0: cachedBedDepthL = 0.5f; break;
                    case 1: cachedBedDepthL = 1.0f; break;
                    case 2: cachedBedDepthL = 1.5f; break;
                    case 3: cachedBedDepthL = 2.0f; break;
                    default: cachedBedDepthL = 1.2f; break;
                }
            }

            // --- LIVE EQUIPMENT OVERVIEW UPDATE ---
            string selectedMaterial = meshMaterialDropdown != null ? meshMaterialDropdown.options[meshMaterialDropdown.value].text : "Unknown";
            string selectedMeshSize = meshOpeningSizeDropdown != null ? meshOpeningSizeDropdown.options[meshOpeningSizeDropdown.value].text : "Unknown";

            if (equipmentMaterialText != null) equipmentMaterialText.text = $"Material: {selectedMaterial}";

            if (equipmentBedDepthText != null) 
                equipmentBedDepthText.text = $"Bed depth: {cachedBedDepthL:F2} m";

            if (equipmentMeshSizeText != null) 
                equipmentMeshSizeText.text = $"Mesh size: {selectedMeshSize}";
            
            // -------------------------------------

            Update3DModelStructure();
            UpdateUnifiedMeshAppearance();
        }
        catch (System.Exception e)
        {
        }
    }

    private void SetFullscreenOverlayActive(bool isTrue)
    {
        if (fullscreenOverlayPanel != null) fullscreenOverlayPanel.SetActive(isTrue);
    }

    private void HandleFullscreen3DClicking()
    {
        try
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Debug.Log($"[System Diagnostics] Operator clicked on: {hit.transform.name}");
                }
            }
        }
        catch (System.Exception e)
        {
        }
    }

    private void OnMainRunButtonClicked()
    {
        if (currentRunState == SimulationState.STANDBY || currentRunState == SimulationState.CONCLUDED)
        {
            StartActiveSimulationRun();
        }
    }

    private void StartActiveSimulationRun()
    {
        try
        {
            currentRunState = SimulationState.RUNNING;
            string matName = meshMaterialDropdown != null ? meshMaterialDropdown.options[meshMaterialDropdown.value].text : "Unknown";
            RecordLogEntry("REACTOR_ENGAGED", $"Operator initiated desulfurization shift. Hardware: {matName} ({cachedBedDepthL:F2} m bed).", "NORMAL");
            runtimeCountdownClock = runtimeCountdownClockStatic;
            meshSaturationAccumulator = 0.0f;

            alarmUpdateTimer = 0f;
            perfSummaryTimer = 1.0f;
            wasInAlarmState = false;
            AddMessageToAlarmLog($"[{System.DateTime.Now.ToString("HH:mm:ss")}] <color=white>Shift Initiated. System polling active.</color>");
            UpdateAlarmHeaderUI(false);

            ToggleStructuralUIInteractability(false);
            ToggleStreamUIInteractability(true);

            SyncRunButtonState("SIMULATING", true);

            if (inletParticles != null && inletParticles.isStopped) inletParticles.Play();
            if (outletParticles != null && outletParticles.isStopped) outletParticles.Play();

            // Enable graphs when starting
            if (efficiencyGraph != null) efficiencyGraph.isSimulationRunning = true;
            if (pressureGraph != null) pressureGraph.isSimulationRunning = true;
            if (outletH2SGraph != null) outletH2SGraph.isSimulationRunning = true;
            if (temperatureGraph != null) temperatureGraph.isSimulationRunning = true;

            SetFullscreenOverlayActive(true);
        }
        catch (System.Exception e)
        {
        }
    }

    private void ProcessSimulationCountdown()
    {
        try
        {
            runtimeCountdownClock -= Time.deltaTime;
            meshSaturationAccumulator += Time.deltaTime / runtimeCountdownClockStatic;

            if (runtimeCountdownClock <= 0.0f)
            {
                FinishAndEvaluateRun();
            }
            else
            {
                SyncRunButtonState($"SIMULATING ({runtimeCountdownClock.ToString("F0")}s)", true);
            }
        }
        catch (System.Exception e)
        {
        }
    }

private void FinishAndEvaluateRun()
    {
        try
        {
            currentRunState = SimulationState.CONCLUDED;
            EvaluateSystemPhysics();

            // Stop all graphs when simulation ends
            if (efficiencyGraph != null) efficiencyGraph.isSimulationRunning = false;
            if (pressureGraph != null) pressureGraph.isSimulationRunning = false;
            if (outletH2SGraph != null) outletH2SGraph.isSimulationRunning = false;
            if (temperatureGraph != null) temperatureGraph.isSimulationRunning = false;

            SyncRunButtonState("ENGAGE REACTOR", true);
            ToggleStructuralUIInteractability(true);

            if (evaluationOverlayPanel != null) evaluationOverlayPanel.transform.SetAsLastSibling();

            // --- UPDATED TEAM LIMITS ---
            string effStatus = cachedEfficiency >= 60f ? "<color=green>[NORMAL]</color>" : (cachedEfficiency >= 50f ? "<color=yellow>[WARNING]</color>" : "<color=red>[FAILURE]</color>");
            string dpStatus = cachedPressureDrop <= 4.0f ? "<color=green>[NORMAL]</color>" : (cachedPressureDrop <= 6.5f ? "<color=yellow>[WARNING]</color>" : "<color=red>[FAILURE]</color>");
            
            // Outlet H2S Limits: Normal < 50, Warning 50-150, Failure > 150
            string h2sStatus = cachedOutletPpm < 50.0f ? "<color=green>[NORMAL]</color>" : (cachedOutletPpm <= 150.0f ? "<color=yellow>[WARNING]</color>" : "<color=red>[FAILURE]</color>");
            
            // Operating Cost Limits: Normal <= 500, Warning 500-1000, Failure > 1000#
            string costStatus = cachedDailyCost <= 500.0f ? "<color=green>[NORMAL]</color>" : (cachedDailyCost <= 1000.0f ? "<color=yellow>[WARNING]</color>" : "<color=red>[FAILURE]</color>");
            
            float tempC = temperatureSlider != null ? temperatureSlider.value : 38f;
            string tempStatus = (tempC >= 25f && tempC <= 45f) ? "<color=green>[NORMAL]</color>" : (tempC <= 60f ? "<color=yellow>[WARNING]</color>" : "<color=red>[FAILURE]</color>");

            bool hasFailures = cachedEfficiency < 50f || cachedPressureDrop > 6.5f || cachedOutletPpm > 150.0f || cachedDailyCost > 1000f || tempC > 60f;
            bool hasWarnings = cachedEfficiency < 60f || (cachedPressureDrop > 4f && cachedPressureDrop <= 6.5f) || (cachedOutletPpm >= 50f && cachedOutletPpm <= 150.0f) || (cachedDailyCost > 500f && cachedDailyCost <= 1000f) || (tempC > 45f && tempC <= 60f);

            string popupTitle = "";
            bool runSuccess = !hasFailures;
            string scenarioName = "Normal Operation";

            if (hasFailures)
            {
                popupTitle = "<color=red><b>SHIFT CONCLUDED - CRITICAL FAILURE</b></color>";
                if (cachedPressureDrop > 6.5f) scenarioName = "High Pressure Drop (Flow Restriction)";
                else if (cachedOutletPpm > 150.0f) scenarioName = "H2S Breakthrough / Toxic Disaster (>150 ppm)";
                else if (cachedDailyCost > 10000f) scenarioName = "Budget Overrun (High Cost)";
                else if (cachedEfficiency < 50f) scenarioName = "Low Removal Efficiency";
                else scenarioName = "Multiple System Failures";
            }
            else if (hasWarnings)
            {
                popupTitle = "<color=yellow><b>SHIFT CONCLUDED - OPERATIONAL WARNING</b></color>";
                scenarioName = "Efficiency Warning / Boundary Limit Reached";
            }
            else
            {
                popupTitle = "<color=green><b>SHIFT CONCLUDED - RUN SUCCESSFUL</b></color>";
                scenarioName = cachedEfficiency > 70f ? "Low-load Efficient Operation" : "Standard Normal Operation";
            }

            string popupMessage = $"<b>Identified Scenario:</b> {scenarioName}\n\n" +
                                  $"<b>Performance Breakdown:</b>\n" +
                                  $"• Removal Efficiency: <b>{cachedEfficiency:F1}%</b> {effStatus}\n" +
                                  $"• Pressure Drop (ΔP): <b>{cachedPressureDrop:F2} kPa</b> {dpStatus}\n" +
                                  $"• Outlet H2S: <b>{cachedOutletPpm:F2} ppm</b> {h2sStatus}\n" +
                                  $"• Operating Temp (T): <b>{tempC:F1} °C</b> {tempStatus}\n" +
                                  $"• Daily Operating Cost: <b>€{cachedDailyCost:F0}/day</b> {costStatus}\n\n" +
                                  (hasFailures ? "<i>Action Required: Adjust stream flow, temperature, or upgrade mesh material to meet safety limits.</i>" : "<i>Great job! Plant parameters remain within safe industrial limits.</i>");

            if (evaluationTitleText != null) evaluationTitleText.text = popupTitle;
            if (evaluationReportText != null) evaluationReportText.text = popupMessage;

            if (restartRunButton != null) restartRunButton.gameObject.SetActive(true);
            if (evaluationOverlayPanel != null) evaluationOverlayPanel.SetActive(true);

            string outcomeSeverity = hasFailures ? "CRITICAL" : (hasWarnings ? "WARNING" : "NORMAL");
            RecordLogEntry("SHIFT_CONCLUDED", $"Shift completed with scenario '{scenarioName}'. Overall result: {(runSuccess ? "PASS" : "FAIL")}.", outcomeSeverity);

            ArchiveRunToHistoryLog(runSuccess, hasFailures ? "FAIL" : (hasWarnings ? "WARN" : "PASS"));
            ClearParticles();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FinishAndEvaluateRun] Error: {e.Message}");
        }
    }

    private void ClearParticles()
    {
        if (inletParticles != null) inletParticles.Stop();
        if (outletParticles != null) outletParticles.Stop();
    }

    private void ResetSimulationToStandby()
    {
        try
        {
            currentRunState = SimulationState.STANDBY;
            runtimeCountdownClock = runtimeCountdownClockStatic;
            meshSaturationAccumulator = 0.0f;

            alarmLogs.Clear();
            alarmUpdateTimer = 0f;
            wasInAlarmState = false;
            if (alarmLogText != null) alarmLogText.text = "<i>Reactor offline. System monitoring standing by...</i>";
            UpdateAlarmHeaderUI(false);

            ResetPerformanceSummaryUI();

            if (evaluationOverlayPanel != null) evaluationOverlayPanel.SetActive(false);
            if (fullscreenOverlayPanel != null && isStartIteration == true)
            { 
                fullscreenOverlayPanel.SetActive(false);
                isStartIteration = false;
            }

            ToggleStructuralUIInteractability(true);
            ToggleStreamUIInteractability(true);

            SyncRunButtonState("ENGAGE REACTOR", true);

            ClearParticles();
            Update3DModelStructure();
            UpdateUnifiedMeshAppearance();

            if (efficiencyGraph != null) efficiencyGraph.isSimulationRunning = false;
            if (pressureGraph != null) pressureGraph.isSimulationRunning = false;
            if (outletH2SGraph != null) outletH2SGraph.isSimulationRunning = false;
            if (temperatureGraph != null) temperatureGraph.isSimulationRunning = false;
        }
        catch (System.Exception e)
        {
        }
    }

    private void ToggleStructuralUIInteractability(bool state)
    {
        try
        {
            if (panel1HardwareCanvasGroup != null)
            {
                panel1HardwareCanvasGroup.interactable = state;
                panel1HardwareCanvasGroup.blocksRaycasts = state;
                panel1HardwareCanvasGroup.alpha = state ? 1.0f : 0.5f;
            }
            else
            {
                if (meshMaterialDropdown != null) meshMaterialDropdown.interactable = state;
                if (discreteBedDepthDropdown != null) discreteBedDepthDropdown.interactable = state;
                if (meshOpeningSizeDropdown != null) meshOpeningSizeDropdown.interactable = state;
                if (btnGenerateModel != null) btnGenerateModel.interactable = state;
            }

            if (expectedEfficiencySlider != null) expectedEfficiencySlider.interactable = state;
            if (estimatedCostSlider != null) estimatedCostSlider.interactable = state;
        }
        catch (System.Exception e)
        {
        }
    }

    private void ToggleStreamUIInteractability(bool state)
    {
        if (gasVolumeSlider != null) gasVolumeSlider.interactable = state;
        if (h2sSlider != null) h2sSlider.interactable = state;
        if (temperatureSlider != null) temperatureSlider.interactable = state;
        if (fullscreenGasVolumeSlider != null) fullscreenGasVolumeSlider.interactable = state;
        if (fullscreenH2SSlider != null) fullscreenH2SSlider.interactable = state;
        if (fullscreenTemperatureSlider != null) fullscreenTemperatureSlider.interactable = state;
    }

    private void SyncRunButtonState(string text, bool isInteractable)
    {
        if (runButtonText != null) runButtonText.text = text;
        if (fullscreenRunButtonText != null) fullscreenRunButtonText.text = text;
        if (mainRunButton != null) mainRunButton.interactable = isInteractable;
        if (fullscreenMainRunButton != null) fullscreenMainRunButton.interactable = isInteractable;
    }

    private void Update3DModelStructure()
    {
        try
        {
            if (catalystMeshes != null && catalystMeshes.Length > 0)
            {
                float targetScaleX = 100f;
                if (discreteBedDepthDropdown != null)
                {
                    switch (discreteBedDepthDropdown.value)
                    {
                        case 0: targetScaleX = 60f; break;
                        case 1: targetScaleX = 70f; break;
                        case 2: targetScaleX = 80f; break;
                        case 3: targetScaleX = 100f; break;
                        default: targetScaleX = 100f; break;
                    }
                }

                for (int i = 0; i < catalystMeshes.Length; i++)
                {
                    if (catalystMeshes[i] != null)
                    {
                        catalystMeshes[i].SetActive(i <= cachedOpeningSizeIndex);
                        catalystMeshes[i].transform.localScale = new Vector3(targetScaleX, 100f, 100f);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
        }
    }

    // ==========================================
    // MATHEMATICAL FRAMEWORK
    // ==========================================
    // ==========================================
    // MATHEMATICAL FRAMEWORK
    // ==========================================
    private void EvaluateSystemPhysics()
    {
        try
        {
            // 1. Read Inputs from Sliders & UI
            float Q_N = gasVolumeSlider != null ? gasVolumeSlider.value : 5000f;       // Nm3/h (500 - 7000)
            float C_in = h2sSlider != null ? h2sSlider.value : 1000f;                  // ppm (500 - 4000)
            float tempC = temperatureSlider != null ? temperatureSlider.value : 38f;   // °C (20 - 65)
            float P_bar = inletPressureSlider != null ? inletPressureSlider.value : 15f; // bar (5 - 45)
            float bedDepthL = cachedBedDepthL;                                         // H (m)

            // 2. Constants & Conversions
            float T = tempC + 273.15f;          // Temperature in Kelvin
            float T_N = 273.15f;                // Normal Temperature (0°C)
            float P_N = 1.01325f;             // Normal Pressure (bar)
            float P = Mathf.Max(P_bar, 0.1f);   // Inlet Pressure (bar)
            float M = 28.97f;                   // Molar mass of gas (g/mol)
            float Z = 0.98f;                    // Compressibility factor
            float R = 8.314f;                   // Universal gas constant

            // Column Dimensions & Opening size mapping
            float columnDiameterD = 1.0f;       // Meters (D)
            float[] openingSizes = { 8.0f, 6.0f, 4.0f }; // d_o in mm based on dropdown index
            int safeOpenIndex = Mathf.Clamp(cachedOpeningSizeIndex, 0, openingSizes.Length - 1);
            float d_o = openingSizes[safeOpenIndex];

            // Material K_mesh mapping based on engineers' table:
            // 0: PTFE-coated stainless steel (1.8), 1: Glass (2.0), 2: Monel alloy (2.2), 3: Titanium (2.1)
            float[] materialKMesh = { 1.8f, 2.0f, 2.2f, 2.1f };
            int safeMatIndex = Mathf.Clamp(cachedMaterialIndex, 0, materialKMesh.Length - 1);
            float K_mesh = materialKMesh[safeMatIndex];

            // 3. Core Formulas from Engineering Model
            // Q_actual = Q_N * (T / T_N) * (P_N / P)
            float Q_actual = Q_N * (T / T_N) * (P_N / P);

            // A = (pi * D^2) / 4
            float A = (Mathf.PI * Mathf.Pow(columnDiameterD, 2.0f)) / 4.0f;

            // V_g = Q_actual / (3600 * A)
            float V_g = Q_actual / (3600.0f * Mathf.Max(A, 0.001f));

            // rho_g = (P * M) / (Z * R * T)
            // Scaled for bar and kg/m3 consistency
            float rho_g = (P * 100f * M) / (Z * R * T); 

            // Delta P = K_mesh * (rho_g * V_g^2) / 2 (converted to kPa)
            //cachedPressureDrop = (K_mesh * rho_g * Mathf.Pow(V_g, 2.0f) / 2.0f) / 1000f;

            // With this scaled and bed-depth-multiplied version:
            float basePressurePa = K_mesh * rho_g * Mathf.Pow(V_g, 2.0f) / 2.0f;
            // Multiply by bed depth (bedDepthL) and a standard industrial flow resistance factor
            cachedPressureDrop = (basePressurePa * bedDepthL * 15.0f) / 1000f; 
            cachedPressureDrop = Mathf.Clamp(cachedPressureDrop, 0.05f, 15.0f);

            // Efficiency eta = 0.50 * (H / 0.50)^0.35 * (5000 / Q_N)^0.25 * (8 / d_o)^0.20 * (38 / T)^0.10
            // Note: Using T_C or T depending on formula normalization; here using tempC
            float etaCalc = 0.50f * Mathf.Pow(bedDepthL / 0.50f, 0.35f)
                                  * Mathf.Pow(5000f / Mathf.Max(Q_N, 1f), 0.25f)
                                  * Mathf.Pow(8.0f / Mathf.Max(d_o, 0.1f), 0.20f)
                                  * Mathf.Pow(38f / Mathf.Max(tempC, 1f), 0.10f);

            cachedEfficiency = Mathf.Clamp(etaCalc * 100f, 0f, 100f);

            // C_out = C_in * (1 - eta)
            cachedOutletPpm = C_in * (1.0f - (cachedEfficiency / 100f));

            // Cost calculations
            float etaComp = 0.75f; // compressor efficiency
            float powerLossKW = (cachedPressureDrop * 1000f * Q_actual / 3600f) / etaComp / 1000f;
            float cEnergy = powerLossKW * 24f * 0.15f; // Daily energy cost
            
            float dailyCapturedH2SKg = (Q_N * C_in * 1.2f * 34.08f) / 1e6f / 3600f * 86400f * (cachedEfficiency / 100f);
            float cMaintenance = dailyCapturedH2SKg * 1.80f;
            
            float[] matBaseCosts = { 120f, 90f, 350f, 250f };
            float cBase = matBaseCosts[safeMatIndex] * bedDepthL;
            float cReplacement = 50f * (100f / Mathf.Max(cachedEfficiency, 1f));

            cachedDailyCost = Mathf.Clamp(cBase + cEnergy + cMaintenance + cReplacement, 0f, 10000f);
            cachedServiceLife = Mathf.Max(10f, 200f - (dailyCapturedH2SKg * 0.5f) - (tempC * 1.2f));

            // 4. Live Telemetry Polling & Visual Updates
            float liveDisplayOutlet = cachedOutletPpm;
            float liveDisplayTemp = tempC;
            float liveDisplayPressure = cachedPressureDrop;
            float liveDisplayEfficiency = cachedEfficiency;

            if (currentRunState == SimulationState.RUNNING)
            {
                float fluctuation = Random.Range(-0.005f, 0.005f);
                liveDisplayEfficiency = Mathf.Clamp(cachedEfficiency * (1f + fluctuation), 0f, 100f);
                liveDisplayPressure = cachedPressureDrop * (1f + fluctuation);
                liveDisplayOutlet = cachedOutletPpm * (1f + fluctuation);
                liveDisplayTemp = tempC + Random.Range(-0.2f, 0.2f);
            }

            if (perfOutletH2SText != null) perfOutletH2SText.text = $"Outlet H2S: {liveDisplayOutlet:F2} ppm";
            if (perfTempText != null) perfTempText.text = $"Temperature: {liveDisplayTemp:F1} °C";
            if (perfPressureText != null) perfPressureText.text = $"Pressure Drop: {liveDisplayPressure:F2} kPa";
            if (perfEfficiencyText != null) perfEfficiencyText.text = $"Efficiency: {liveDisplayEfficiency:F1}%";

            UpdateUserInterfaceDisplay(cachedEfficiency, cachedDailyCost, cachedPressureDrop, cachedOutletPpm, cachedServiceLife);
            UpdateUnifiedMeshAppearance();

            // Particle Flow updates
            if (inletParticles != null)
            {
                var mainModule = inletParticles.main;
                var emissionModule = inletParticles.emission;
                mainModule.startSpeed = (Q_actual / 3600f) * 2.0f;
                emissionModule.rateOverTime = currentRunState == SimulationState.RUNNING ? Mathf.Lerp(20f, 120f, Mathf.InverseLerp(500f, 4000f, C_in)) : 0f;
            }

            if (outletParticles != null)
            {
                var mainModule = outletParticles.main;
                var emissionModule = outletParticles.emission;
                mainModule.startSpeed = (Q_actual / 3600f) * 2.5f;
                emissionModule.rateOverTime = (currentRunState == SimulationState.RUNNING && inletParticles != null) ? inletParticles.emission.rateOverTime.constant : 0f;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[EvaluateSystemPhysics] Error: {e.Message}");
        }
    }

    private Color GetMaterialBaseColor(int materialIndex)
    {
        switch (materialIndex)
        {
            case 0: return new Color(0.75f, 0.75f, 0.78f);
            case 1: return new Color(0.65f, 0.70f, 0.65f);
            case 2: return new Color(0.95f, 0.95f, 0.95f);
            case 3: return new Color(0.60f, 0.60f, 0.60f);
            default: return Color.gray;
        }
    }

    private void UpdateUnifiedMeshAppearance()
    {
        try
        {
            Color baseMatColor = GetMaterialBaseColor(cachedMaterialIndex);
            Color rustColor = new Color(0.55f, 0.30f, 0.15f, 1f);

            float saturationFactor = Mathf.Clamp01(meshSaturationAccumulator);
            Color rustedColor = Color.Lerp(baseMatColor, rustColor, saturationFactor);

            float currentTemp = temperatureSlider != null ? temperatureSlider.value : 35f;
            float tempNormalized = Mathf.Clamp01(Mathf.InverseLerp(35f, 200f, currentTemp));
            Color tempInfluencedColor = Color.Lerp(rustedColor, heatedReactorColor, tempNormalized);

            float safetyAlertFactor = 0f;
            if (currentRunState != SimulationState.STANDBY)
            {
                safetyAlertFactor = Mathf.Clamp01(Mathf.InverseLerp(0f, 6.5f, cachedPressureDrop));
            }

            Color finalInnerMeshColor = Color.Lerp(tempInfluencedColor, Color.red, safetyAlertFactor);

            if (instancedMaterials != null)
            {
                for (int i = 0; i < instancedMaterials.Length; i++)
                {
                    if (instancedMaterials[i] != null)
                    {
                        instancedMaterials[i].color = finalInnerMeshColor;
                        instancedMaterials[i].SetColor("_BaseColor", finalInnerMeshColor);
                    }
                }
            }

            if (reactorMaterial != null)
            {
                Color targetReactorColor = Color.Lerp(baseReactorColor, heatedReactorColor, tempNormalized);
                reactorMaterial.color = targetReactorColor;
                reactorMaterial.SetColor("_BaseColor", targetReactorColor);
            }
        }
        catch (System.Exception e)
        {
        }
    }

    private void UpdateUserInterfaceDisplay(float eff, float cost, float pressDrop, float outPpm, float days)
    {
        try
        {
            if (rightSideEfficiencyText != null) rightSideEfficiencyText.text = $"{eff.ToString("F1")}%";
            if (rightSideCostText != null) rightSideCostText.text = $"€{cost.ToString("F0")} / day";

            if (detailedPressureDropText != null) detailedPressureDropText.text = $"Pressure Drop: {pressDrop.ToString("F2")} kPa";
            if (detailedOutletPpmText != null) detailedOutletPpmText.text = $"Outlet H2S: {outPpm.ToString("F2")} ppm";
            if (detailedServiceLifeText != null) detailedServiceLifeText.text = $"Service Life: {Mathf.Max(0, days).ToString("F0")} Days";

            if (detailedComplianceStatusText != null)
            {
                if (outPpm > 50.0f || pressDrop > 6.5f || cost > 3000f)
                {
                    detailedComplianceStatusText.text = currentRunState == SimulationState.RUNNING ? "Status: SYSTEM UNDER DURESS" : "Status: NON-COMPLIANT";
                    detailedComplianceStatusText.color = Color.red;
                }
                else
                {
                    detailedComplianceStatusText.text = currentRunState == SimulationState.STANDBY ? "OFFLINE STANDBY" : "Status: OPERATIONAL (SECURE)";
                    detailedComplianceStatusText.color = currentRunState == SimulationState.STANDBY ? Color.white : Color.green;
                }
            }

            if (graphBoundingBox != null && graphTrackingNode != null)
            {
                float normalizedX = Mathf.InverseLerp(0f, 100f, eff);
                float normalizedY = Mathf.InverseLerp(0f, 8000f, cost);

                float targetX = normalizedX * graphBoundingBox.rect.width;
                float targetY = normalizedY * graphBoundingBox.rect.height;

                float padding = 10f;
                float clampedX = Mathf.Clamp(targetX, padding, graphBoundingBox.rect.width - padding);
                float clampedY = Mathf.Clamp(targetY, padding, graphBoundingBox.rect.height - padding);

                graphTrackingNode.anchoredPosition = new Vector2(clampedX, clampedY);
            }

            float expectedEff = expectedEfficiencySlider != null ? expectedEfficiencySlider.value : 100f;
            float currentTemp = temperatureSlider != null ? temperatureSlider.value : 55f;

            if (efficiencyGraph != null) efficiencyGraph.UpdateTelemetry(eff, expectedEff);
            if (pressureGraph != null) pressureGraph.UpdateTelemetry(pressDrop, 6.5f);
            if (outletH2SGraph != null) outletH2SGraph.UpdateTelemetry(outPpm, 50.0f);
            if (temperatureGraph != null) temperatureGraph.UpdateTelemetry(currentTemp, 55.0f);
        }
        catch (System.Exception e)
        {
        }
    }

    private void QuitRefinerySimulator()
    {
        Application.Quit();
    }

    private void HandleZoom()
    {
        try
        {
            if (Time.timeScale == 0f) return;

            if (studioCamera != null && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            {
                if (studioCamera.orthographic)
                {
                    studioCamera.orthographicSize = Mathf.Clamp(studioCamera.orthographicSize - Input.mouseScrollDelta.y * zoomSpeed, minZoom, maxZoom);
                }
                else
                {
                    studioCamera.transform.Translate(Vector3.forward * Input.mouseScrollDelta.y * zoomSpeed, Space.Self);
                    Vector3 pos = studioCamera.transform.localPosition;
                    pos.z = Mathf.Clamp(pos.z, -maxZoom, -minZoom);
                    studioCamera.transform.localPosition = pos;
                }
            }
        }
        catch (System.Exception e)
        {
        }
    }

    // ==========================================
    // OPERATIONAL AUDIT & USER LOGGING ENGINE
    // ==========================================
    public void RecordLogEntry(string eventTag, string eventDescription, string severity = "NORMAL")
    {
        try
        {
            string timeStamp = System.DateTime.Now.ToString("HH:mm:ss");
            string colorTag = severity switch
            {
                "CRITICAL" => "<color=#FF4444>[CRITICAL]</color>",
                "WARNING"  => "<color=#FFCC00>[WARNING]</color>",
                _          => "<color=#00FF66>[NORMAL]</color>"
            };

            float Q_N = gasVolumeSlider != null ? gasVolumeSlider.value : 5000f;
            float P_bar = inletPressureSlider != null ? inletPressureSlider.value : 15f;
            float tempC = temperatureSlider != null ? temperatureSlider.value : 38f;
            float C_in = h2sSlider != null ? h2sSlider.value : 1000f;

            string entry = $"<b>[{timeStamp}]</b> {colorTag} <b>{eventTag}</b>: {eventDescription}\n" +
                           $"   <color=#88AACC>↳ Telemetry:</color> Flow: {Q_N:F0} Nm³/h | Press: {P_bar:F1} bar | Temp: {tempC:F1} °C\n" +
                           $"   <color=#88AACC>↳ Performance:</color> Cin: {C_in:F0} ppm ➔ Cout: {cachedOutletPpm:F2} ppm | Eff: {cachedEfficiency:F1}% | ΔP: {cachedPressureDrop:F3} kPa";

            operationalAuditLog.Insert(0, entry);

            while (operationalAuditLog.Count > MaxLogEntries)
            {
                operationalAuditLog.RemoveAt(operationalAuditLog.Count - 1);
            }

            SaveAuditLogsToStorage();
            UpdateLogDisplayUI();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RecordLogEntry] Error: {e.Message}");
        }
    }

    public void UpdateLogDisplayUI()
    {
        if (logContentText == null) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<size=16><b>=== REFINERY SCADA AUDIT TRAIL & OPERATOR LOGS ===</b></size>\n");

        for (int i = 0; i < operationalAuditLog.Count; i++)
        {
            sb.AppendLine(operationalAuditLog[i]);
            sb.AppendLine("<color=#223344>─────────────────────────────────────────────────────────────────</color>");
        }

        logContentText.text = sb.ToString();

        if (logSummaryHeader != null)
        {
            logSummaryHeader.text = $"AUDIT EVENTS: {operationalAuditLog.Count} | LOG STATUS: ACTIVE";
        }
    }

    public void ToggleUserLogsModal()
    {
        if (userLogsModal != null)
        {
            SetUserLogsModalActive(!userLogsModal.activeSelf);
        }
    }

    public void SetUserLogsModalActive(bool state)
    {
        if (userLogsModal != null)
        {
            userLogsModal.SetActive(state);
            if (state)
            {
                userLogsModal.transform.SetAsLastSibling();
                UpdateLogDisplayUI();
            }
        }
    }

    public void ClearUserLogs()
    {
        operationalAuditLog.Clear();
        PlayerPrefs.DeleteKey(AUDIT_LOG_PREF_KEY);
        PlayerPrefs.Save();
        RecordLogEntry("AUDIT_RESET", "Operator manually purged session log records.", "WARNING");
    }

    private void SaveAuditLogsToStorage()
    {
        try
        {
            string serialized = string.Join("|||", operationalAuditLog);
            PlayerPrefs.SetString(AUDIT_LOG_PREF_KEY, serialized);
            PlayerPrefs.Save();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveAuditLogs] Error: {e.Message}");
        }
    }

    private void LoadAuditLogsFromStorage()
    {
        try
        {
            if (PlayerPrefs.HasKey(AUDIT_LOG_PREF_KEY))
            {
                string raw = PlayerPrefs.GetString(AUDIT_LOG_PREF_KEY);
                if (!string.IsNullOrEmpty(raw))
                {
                    string[] records = raw.Split(new string[] { "|||" }, System.StringSplitOptions.RemoveEmptyEntries);
                    operationalAuditLog = new List<string>(records);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LoadAuditLogs] Error: {e.Message}");
        }
    }
}