# EcoMesh 2.0: Interactive Refinery SCADA Simulator

[![Unity](https://img.shields.io/badge/Unity-6000.4.8f1%20(6)-black?logo=unity)](https://unity.com/)
[![Language](https://img.shields.io/badge/C%23-12%20%2F%20.NET-blue?logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP-informational)](https://unity.com/srp/universal-render-pipeline)
[![Safety Standard](https://img.shields.io/badge/Safety%20Threshold-H₂S%20%3C%2050%20ppm-success)](https://github.com)

**EcoMesh 2.0** is an interactive, real-time industrial digital twin and Supervisory Control and Data Acquisition (SCADA) simulator for an industrial gas-sweetening column. Developed in Unity 6, the system simulates sour gas desulfurization ($H_2S$ removal) across porous catalytic mesh matrices by coupling hydrodynamics, reaction mass transfer kinetics, and automated plant safety logic into an operational SCADA interface.

> **Educational Prototype Notice:** Calculations, telemetry readouts, and status alarms are illustrative outputs developed for simulation and training. This tool is not validated for live plant design, hazardous chemical commissioning, or regulatory compliance sign-offs.

---

## 🏭 Process & Physics Overview

The simulation models the counter-current gas-solid desulfurization of sour gas inside a fixed-bed catalytic reactor:

### 1. Gas Flow Correction & Superficial Velocity
Inlet volumetric standard flow ($\text{Nm}^3/\text{h}$) is dynamically adjusted for operating temperature ($T$) and vessel pressure ($P$) using the ideal gas law:

$$Q = \left(\frac{\dot{V}_{\text{normal}}}{3600}\right) \cdot \left(\frac{T}{273.15}\right) \cdot \left(\frac{1.01325}{P_{\text{actual}}}\right) \quad [\text{m}^3/\text{s}]$$

Superficial gas velocity through the cylindrical column (diameter $D$) is given by:

$$u = \frac{Q}{\frac{\pi D^2}{4}} \quad [\text{m}/\text{s}]$$

### 2. Bed Hydrodynamics & Pressure Drop ($\Delta P$)
Gas flow hydrodynamics and differential pressure across the structured packing of depth $L$ are evaluated via the **Ergun equation**:

$$\frac{\Delta P}{L} = f_r \cdot \left[ 150 \frac{(1 - \varepsilon)^2}{\varepsilon^3} \frac{\mu u}{d_p^2} + 1.75 \frac{1 - \varepsilon}{\varepsilon^3} \frac{\rho u^2}{d_p} \right] \quad [\text{Pa}]$$

where $\varepsilon$ is bed void fraction, $d_p$ is aperture/particle size, $\mu$ is dynamic viscosity, and $f_r$ is the material-specific friction factor.

### 3. Mass Transfer & Reaction Kinetics
* **Gas-Film Coefficient ($k_g$):** Resolved via Wakao-Kaguei convective correlations.
* **Surface Kinetics ($k_r$):** Temperature-dependent Arrhenius reaction rate accounting for catalytic site saturation:

$$k_r = f_m \cdot k_{r0} \cdot \exp\left(-\frac{E_a}{R T}\right) \cdot \frac{1}{1 + \frac{c_{in}}{C_{\text{sat}}}} \quad [\text{m}/\text{s}]$$

* **Transfer Units (NTU):** Bed transfer efficiency is computed over the dual resistance model ($\frac{1}{K_{ov}} = \frac{1}{k_g} + \frac{1}{k_r}$):

$$\text{NTU} = K_{ov} \cdot a_v \cdot \frac{L}{u}$$

### 4. Separation Efficiency & Regulatory Thresholds
Overall removal efficiency ($\eta$) and effluent concentration ($c_{out}$) are modeled assuming plug-flow conditions:

$$\eta = 1 - \exp(-\text{NTU}), \quad c_{out} = c_{in} \cdot (1 - \eta) \quad [\text{ppmv}]$$

* **Normal / Compliant:** $c_{out} < 50.0\text{ ppm}$
* **Warning Alarm:** $50.0\text{ ppm} \le c_{out} < 100.0\text{ ppm}$
* **Critical Alarm / Toxic Excursion:** $c_{out} \ge 100.0\text{ ppm}$ or $\Delta P > \Delta P_{\text{max}}$

---

## 🛠️ Key Technical Contributions

* **Real-Time SCADA Telemetry & UI:** Designed and engineered the interactive multi-tab operator console, live parameter sliders, visual status annunciators, and high-contrast alert displays using Unity UGUI and TextMesh Pro.
* **Dynamic Strip-Chart Graphing:** Integrated real-time line graphs to plot transient telemetry curves, process fluctuations, and response rates during active runs.
* **Safety Logic & Threshold Calibration:** Standardized alarm triggers, status flags, and telemetry diagnostics to strictly conform with the safe $< 50\text{ ppm}$ $H_2S$ outlet compliance standard.
* **Shift Audit Logging System:** Built the backend logging architecture in `UnifiedRefinerySimulator.cs`, providing a persistent modal audit log that records operator setpoints, desulfurization efficiency, operating cost ($\text{EUR/day}$), and shift diagnostics across up to 10 sequential runs (via `PlayerPrefs`).
* **3D Reactor Modeling & Presentation:** Integrated custom 3D reactor models (flanges, viewing window, pressure taps) within the Universal Render Pipeline (URP).

---

## 💻 Tech Stack & Architecture

* **Engine:** Unity 6 (`6000.4.8f1`)
* **Language:** C# 12 / .NET
* **UI & Rendering:** Unity Canvas UGUI, TextMesh Pro, Universal Render Pipeline (URP)
* **3D Modeling:** Blender
* **Version Control:** Git / GitHub

---

## 📁 Repository Structure

```text
EcoMesh2.0/
├── Assets/
│   ├── _Project/
│   │   ├── Data/
│   │   │   └── Materials/      # Material profile definitions
│   │   ├── Models/             # 3D reactor vessel, inspection port, and gauges
│   │   └── Scripts/            # Core logic, UI controllers, and plotting routines
│   │       ├── UnifiedRefinerySimulator.cs  # Physics, alarm logic, and shift logs
│   │       ├── TelemetryGraph.cs            # Live telemetry graph plotter
│   │       └── DashboardController.cs       # UI tab management and modal windows
│   └── Scenes/
│       └── SampleScene.unity   # Primary interactive SCADA scene
├── Packages/                   # Package manifests and locked dependencies
└── ProjectSettings/            # Physics, input, and project configurations
```

---

## 🚀 Getting Started

### Prerequisites
* **Unity Hub** installed.
* **Unity Editor 6000.4.8f1** (Unity 6).
* Git configured with Large File Storage (LFS) support.

### Setup and Running
1. Clone the repository:
   ```bash
   git clone https://github.com/Danish-Rahim/EcoMesh2.0.git
   ```
2. Open **Unity Hub**, click **Add**, and select the cloned root folder.
3. Open the project using **Unity 6000.4.8f1** and allow package resolution to complete.
4. Navigate to `Assets/Scenes/SampleScene.unity` and open it.
5. Press **Play** in the editor.
6. Configure the reactor geometry and inlet feed sliders, then press **Engage Reactor** to trigger an 11-second operational shift and inspect the performance report.

---

## 📄 License

This repository is maintained for academic and educational evaluation. All rights reserved.
