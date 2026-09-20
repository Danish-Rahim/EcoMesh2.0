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

    static private float runtimeCountdownClockStatic = 20.0f;
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

    [Header("Fullscreen Mirrored Controls")]
    public Slider fullscreenGasVolumeSlider;
    public Slider fullscreenH2SSlider;
    public Slider fullscreenTemperatureSlider;
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
            if (closeEvaluationPopupButton != null) closeEvaluationPopupButton.onClick.AddListener(CloseEvaluationPopup);
            if (quitApplicationButton != null) quitApplicationButton.onClick.AddListener(QuitRefinerySimulator);
            if (maximizeViewportButton != null) maximizeViewportButton.onClick.AddListener(() => SetFullscreenOverlayActive(true));
            if (closeFullscreenButton != null) closeFullscreenButton.onClick.AddListener(() => SetFullscreenOverlayActive(false));

            SyncSliders(gasVolumeSlider, fullscreenGasVolumeSlider);
            SyncSliders(h2sSlider, fullscreenH2SSlider);
            SyncSliders(temperatureSlider, fullscreenTemperatureSlider);

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
            if (cachedOutletPpm > 5.0f) activeWarnings.Add($"Toxic Leak ({cachedOutletPpm:F1} ppm)");
            if (cachedDailyCost > 3000f) activeWarnings.Add($"Budget Overflow (€{cachedDailyCost:F0})");

            string timeStamp = System.DateTime.Now.ToString("HH:mm:ss");
            bool isInAlarmState = activeWarnings.Count > 0;

            if (isInAlarmState)
            {
                string combinedWarnings = string.Join(" | ", activeWarnings);
                AddMessageToAlarmLog($"[{timeStamp}] <color=red>WARNING: {combinedWarnings}</color>");
                UpdateAlarmHeaderUI(true);
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
                if (graphHeaderSafeOutletText != null) graphHeaderSafeOutletText.text = "Safe: < 5.0 ppm";

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
            if (graphHeaderSafeOutletText != null) graphHeaderSafeOutletText.text = "Safe: < 5.0 ppm";

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

            // Restore Run Button to original state and make it interactable again
            SyncRunButtonState("ENGAGE REACTOR", true);
            ToggleStructuralUIInteractability(true);

            if (evaluationOverlayPanel != null) evaluationOverlayPanel.transform.SetAsLastSibling();

            string popupTitle = "";
            string popupMessage = "";
            bool runSuccess = false;
            string grade = "F";

            float expectedEff = expectedEfficiencySlider != null ? expectedEfficiencySlider.value : 85f;
            float expectedCost = estimatedCostSlider != null ? estimatedCostSlider.value : 1500f;

            if (cachedPressureDrop > 6.5f)
            {
                popupTitle = "<color=red>CRITICAL PLANT DISASTER</color>";
                popupMessage = $"<b>RUN FAILED</b>\n\nCatastrophic structural failure! Pressure drop hit {cachedPressureDrop:F2} kPa, exceeding casing limits.";
            }
            else if (cachedOutletPpm > 5.0f)
            {
                popupTitle = "<color=red>CRITICAL PLANT DISASTER</color>";
                popupMessage = $"<b>RUN FAILED</b>\n\nToxic venting breach! Outlet concentrations hit {cachedOutletPpm:F1} ppm, violating EPA standards.";
            }
            else if (cachedDailyCost > 3000f)
            {
                popupTitle = "<color=yellow>BUDGET OVERRUN</color>";
                popupMessage = $"<b>RUN FAILED</b>\n\nSystem operates safely but exceeds daily operating budget.";
            }
            else
            {
                runSuccess = true;
                float effVariance = Mathf.Abs(cachedEfficiency - expectedEff);
                float costVariance = Mathf.Abs(cachedDailyCost - expectedCost);

                if (effVariance <= 2.0f && costVariance <= 200f) grade = "A+";
                else if (effVariance <= 5.0f && costVariance <= 500f) grade = "A";
                else if (effVariance <= 10.0f && costVariance <= 800f) grade = "B";
                else grade = "C";

                popupTitle = $"<color=green>SHIFT SUCCESS - GRADE {grade}</color>";
                popupMessage = $"<b>CONGRATULATIONS, OPERATOR!</b>\n\nReactor operates safely.\n\n<b>Estimation Accuracy:</b>\nEfficiency Variance: {effVariance:F1}%\nCost Variance: €{costVariance:F0}";
            }

            if (evaluationTitleText != null) evaluationTitleText.text = popupTitle;
            if (evaluationReportText != null)
            {
                evaluationReportText.text = $"{popupMessage}\n\n<b>Final Telemetry Snapshot:</b>\n" +
                                            $"Efficiency: {cachedEfficiency:F1}%\n" +
                                            $"Real-time Operational Cost: €{cachedDailyCost:F2}/day";
            }

            if (restartRunButton != null) restartRunButton.gameObject.SetActive(true);
            if (evaluationOverlayPanel != null) evaluationOverlayPanel.SetActive(true);

            ArchiveRunToHistoryLog(runSuccess, grade);
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
    private void EvaluateSystemPhysics()
    {
        try
        {
            float columnArea = 2.0f;
            float baseKineticK = 1.29f;

            float gasFlowQ = gasVolumeSlider != null ? gasVolumeSlider.value : 750f;
            float inletH2S = h2sSlider != null ? h2sSlider.value : 850f;
            float tempC = temperatureSlider != null ? temperatureSlider.value : 55f;
            float bedDepthL = cachedBedDepthL;

            float[] matKinetics = { 1.0f, 0.8f, 1.3f, 1.1f };
            float[] matDurability = { 1.0f, 0.6f, 2.0f, 0.8f };
            float[] matBaseCost = { 100f, 50f, 400f, 150f };

            float[] openingArea = { 1.4f, 1.0f, 0.7f };
            float[] openingDrop = { 1.6f, 1.0f, 0.5f };

            int safeMatIndex = Mathf.Clamp(cachedMaterialIndex, 0, 3);
            int safeOpenIndex = Mathf.Clamp(cachedOpeningSizeIndex, 0, 2);

            float superficialVelocity = (gasFlowQ / 3600f) / columnArea;
            float gasContactTime = bedDepthL / (superficialVelocity > 0 ? superficialVelocity : 0.0001f);

            cachedPressureDrop = (1.5f * superficialVelocity + 0.5f * Mathf.Pow(superficialVelocity, 2))
                                 * bedDepthL
                                 * openingDrop[safeOpenIndex];

            float tempModifier = 1.0f + ((tempC - 55f) * 0.02f);
            float adjustedK = baseKineticK * tempModifier * matKinetics[safeMatIndex] * openingArea[safeOpenIndex];

            cachedEfficiency = 100f * (1f - Mathf.Exp(-adjustedK * gasContactTime));
            cachedEfficiency = Mathf.Clamp(cachedEfficiency, 0f, 99.99f);
            cachedOutletPpm = inletH2S * (1f - (cachedEfficiency / 100f));

            float blowerPowerKW = (gasFlowQ / 3600f * (cachedPressureDrop * 1000f)) / 0.75f / 1000f;
            float dailyEnergyCost = blowerPowerKW * 24f * 0.15f;
            float dailyCapturedH2SKg = (gasFlowQ * inletH2S * 1.2f * 34.08f) / 1e6f / 3600f * 86400f * (cachedEfficiency / 100f);
            float dailyRegenerationCost = dailyCapturedH2SKg * 1.80f;
            float structuralDepreciation = matBaseCost[safeMatIndex];

            float rawDailyCost = dailyEnergyCost + dailyRegenerationCost + structuralDepreciation;
            cachedDailyCost = Mathf.Clamp(rawDailyCost, 0f, 10000f);
            cachedServiceLife = (145f * matDurability[safeMatIndex]) - (dailyCapturedH2SKg * 0.1f);

            // Live Output Card real-time telemetry calculation
            float liveDisplayOutlet = cachedOutletPpm;
            float liveDisplayTemp = tempC;
            float liveDisplayPressure = cachedPressureDrop;
            float liveDisplayEfficiency = cachedEfficiency;

            if (currentRunState == SimulationState.RUNNING)
            {
                float fluctuation = Random.Range(-0.008f, 0.008f);
                liveDisplayEfficiency = Mathf.Clamp(cachedEfficiency * (1f + fluctuation), 0f, 99.99f);
                liveDisplayPressure = cachedPressureDrop * (1f + fluctuation);
                liveDisplayOutlet = cachedOutletPpm * (1f + fluctuation);
                liveDisplayTemp = tempC + Random.Range(-0.3f, 0.3f);
            }

            if (perfOutletH2SText != null) perfOutletH2SText.text = $"Outlet H2S: {liveDisplayOutlet:F2} ppm";
            if (perfTempText != null) perfTempText.text = $"Temperature: {liveDisplayTemp:F1} °C";
            if (perfPressureText != null) perfPressureText.text = $"Pressure Drop: {liveDisplayPressure:F2} kPa";
            if (perfEfficiencyText != null) perfEfficiencyText.text = $"Efficiency: {liveDisplayEfficiency:F1}%";

            UpdateUserInterfaceDisplay(cachedEfficiency, cachedDailyCost, cachedPressureDrop, cachedOutletPpm, cachedServiceLife);
            UpdateUnifiedMeshAppearance();

            if (inletParticles != null)
            {
                var mainModule = inletParticles.main;
                var emissionModule = inletParticles.emission;

                mainModule.startSpeed = (gasFlowQ / 3600f) * 2.0f;
                emissionModule.rateOverTime = currentRunState == SimulationState.RUNNING ? Mathf.Lerp(20f, 120f, Mathf.InverseLerp(0f, 2000f, inletH2S)) : 0f;

                float toxicityFactor = Mathf.InverseLerp(0f, 2000f, inletH2S);
                mainModule.startColor = Color.Lerp(new Color(0.5f, 0.45f, 0.3f, 0.4f), new Color(0.75f, 0.55f, 0.1f, 0.75f), toxicityFactor);
            }

            if (outletParticles != null)
            {
                var mainModule = outletParticles.main;
                var emissionModule = outletParticles.emission;

                mainModule.startSpeed = (gasFlowQ / 3600f) * 2.5f;
                emissionModule.rateOverTime = (currentRunState == SimulationState.RUNNING && inletParticles != null) ? inletParticles.emission.rateOverTime.constant : 0f;

                float efficiencyRatio = cachedEfficiency / 100f;
                Color cleanAirColor = new Color(0.4f, 0.75f, 1.0f, 0.3f);
                Color bypassTaintedColor = new Color(0.65f, 0.5f, 0.15f, 0.6f);
                mainModule.startColor = Color.Lerp(bypassTaintedColor, cleanAirColor, efficiencyRatio);
            }
        }
        catch (System.Exception e)
        {
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
                if (outPpm > 5.0f || pressDrop > 6.5f || cost > 3000f)
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
            if (outletH2SGraph != null) outletH2SGraph.UpdateTelemetry(outPpm, 5.0f);
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
}