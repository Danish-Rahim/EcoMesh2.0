# EcoMesh 2.0: Interactive Refinery SCADA Simulator

[![Unity](https://img.shields.io/badge/Unity-6000.4.8f1-black.svg?style=flat&logo=unity)](https://unity.com/)
[![C#](https://img.shields.io/badge/Language-C%23%2012-blue.svg?style=flat&logo=c-sharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Physics](https://img.shields.io/badge/Physics-Ergun%20%7C%20NTU%20Mass%20Transfer-orange.svg)]()
[![Compliance](https://img.shields.io/badge/Safety%20Standard-H2S%20%3C%2050%20ppm-green.svg)]()

**EcoMesh 2.0** is an interactive, real-time industrial digital twin and supervisory control and data acquisition (SCADA) simulator for an industrial desulfurization column. Developed in Unity 6, the system simulates sour gas sweetening processes by coupling bed hydrodynamics, mass transfer kinetics, and plant safety logic into an operational SCADA dashboard.

---

## 🏭 Process & Physics Overview

The simulation models the counter-current gas-solid desulfurization of sour gas using porous catalytic mesh matrices:

1. **Hydrodynamics & Bed Pressure Drop ($\Delta P$):**
   Gas flow hydrodynamics across structured porous mesh packing are calculated using the classic **Ergun Equation**:
   $$\frac{\Delta P}{L} = 150 \frac{(1 - \varepsilon)^2}{\varepsilon^3} \frac{\mu u}{d_p^2} + 1.75 \frac{1 - \varepsilon}{\varepsilon^3} \frac{\rho u^2}{d_p}$$
   where operational volumetric flow $Q$ is pressure- and temperature-compensated to reflect actual superficial velocities ($u$).

2. **Mass Transfer & Reaction Kinetics:**
   * **Gas-Film Transfer:** Gas-phase mass transfer coefficients ($k_g$) are resolved via Wakao-Kaguei correlations.
   * **Surface Reaction:** Arrhenius temperature-dependent kinetics ($k_r$) account for surface catalytic conversion with saturation term limits ($C_{sat}$).
   * **Transfer Units (NTU):** Total bed efficiency is evaluated across combined resistance:
     $$\text{NTU} = \left(\frac{1}{k_g} + \frac{1}{k_r}\right)^{-1} \cdot \frac{a_v \cdot L}{u}$$
     $$\eta = 1 - e^{-\text{NTU}}, \quad C_{\text{out}} = C_{\text{in}} (1 - \eta)$$

3. **Safety & Regulatory Compliance:**
   * **Normal/Safe Limit:** $C_{\text{out}} < 50.0\text{ ppm}$ $H_2S$
   * **Warning / Alert:** $50.0\text{ ppm} \le C_{\text{out}} < 100.0\text{ ppm}$
   * **Critical Shutdown / Toxic Threshold:** $C_{\text{out}} \ge 100.0\text{ ppm}$ or excessive $\Delta P$ (bed fouling/blowout risk)

---

## 🛠️ Key Technical Contributions

* **SCADA UI & Telemetry Integration:** Engineered the real-time operational dashboard, multi-tab monitoring interface, and dynamic line plotting for continuous stream analysis.
* **Safety & Diagnostic Logic:** Standardized all telemetry alerts, alarm matrix thresholds, and visual warning triggers around industrial safety standards ($< 50\text{ ppm}$ $H_2S$ compliance threshold).
* **Shift Audit Backend:** Implemented persistent shift logging and diagnostic auditing in `UnifiedRefinerySimulator.cs`, tracking operator setpoints, total desulfurization efficiency, and cumulative operating costs ($EUR/\text{day}$).
* **3D Inspection & Spatial Setup:** Integrated high-fidelity 3D reactor models (flanges, transparent observation port, differential pressure sensors) within the Unity Universal Render Pipeline (URP).

---

## 💻 Tech Stack & Architecture

* **Engine:** Unity 6 (`6000.4.8f1`)
* **Logic / Scripting:** C# (.NET 8 / C# 12)
* **UI & Rendering:** Unity Canvas UGUI, TextMesh Pro, Universal Render Pipeline (URP)
* **3D Assets:** Blender
* **Version Control:** Git / GitHub

---

## 📁 Repository Structure

```text
EcoMesh2.0/
├── Assets/
│   ├── _Project/
│   │   ├── Scripts/
│   │   │   └── UnifiedRefinerySimulator.cs   # Core process physics, UI controller & shift logging
│   │   └── Models/                           # 3D reactor, mesh beds, and plant geometry
│   ├── Scenes/
│   │   └── SampleScene.unity                 # Main interactive SCADA environment
│   └── TextMesh Pro/                         # Fonts, materials, and SDF UI shader graph assets
├── Packages/                                 # Unity package manifests
└── ProjectSettings/                          # Engine settings, input tags, and quality configurations
```

---

## 🚀 Getting Started

1. **Clone the repository:**
   ```bash
   git clone https://github.com/Danish-Rahim/EcoMesh2.0.git
   ```
2. **Open in Unity Hub:**
   * Unity Version: `6000.4.8f1`
   * Target Platform: PC, Mac & Linux Standalone
3. **Run the Simulation:**
   * Open `Assets/Scenes/SampleScene.unity`.
   * Press **Play** in the editor. Adjust inlet gas parameters (flow, concentration, pressure, temperature) and bed aperture, then monitor real-time compliance readouts and engage the reactor.
