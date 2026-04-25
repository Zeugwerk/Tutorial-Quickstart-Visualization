# Zeugwerk Framework Quickstart Tutorial - 3D Visualization

A Godot 3 application that connects to the [Zeugwerk Framework Quickstart Tutorial](https://github.com/Zeugwerk/Tutorial-Quickstart) PLC project via TwinCAT ADS. It provides an interactive 3D scene of the machine with a physics simulation, so the Quickstart Tutorial can be explored without any physical hardware.

![Quickstart automatic sequence running](docs/quickstart_automatic_sequence.gif)

The scene shows the full machine: a conveyor with left and right limit switches, a pick-and-place transport axis with a pneumatic cylinder and magnet, and a control panel for manual overrides and sequence commands (Start, Stop, GoHome, Halt).

The visualization acts as a digital twin: it reads and drives digital inputs in the PLC (`LimitSwitchLeft`, `LimitSwitchRight`, `CylinderYIsDown`, `CylinderYIsUp`) based on the physics simulation, so the PLC logic runs as if real hardware is present.

## Prerequisites

- [Godot 3](https://godotengine.org/) with Mono / C# support (Godot 3.5 recommended)
- [TwinCAT 3.1](https://www.beckhoff.com/twincat3) runtime with the [Tutorial-Quickstart](https://github.com/Zeugwerk/Tutorial-Quickstart) PLC project running
- ADS route configured between the machine running Godot and the TwinCAT runtime

## Getting started

1. Clone the repository
2. Set up and run the [Tutorial-Quickstart](https://github.com/Zeugwerk/Tutorial-Quickstart) PLC project first
3. Open `project.godot` in Godot 3 (Mono version)
4. Run the scene (`Spatial.tscn`)
5. Enter the AMS Net ID of your TwinCAT runtime in the connection field (leave blank for a local runtime) and click **Connect**

The visualization connects to `ZGlobal.Com.Unit.Quickstart.*` in the PLC. Once connected, the control panel buttons write request bits directly into the PLC, and the 3D scene reflects the resulting equipment states in real time.

If the cube falls off the conveyor during simulation, press **Reset cube** to reposition it at the left limit switch.

Full walkthrough of the machine and its sequences is in the [Quickstart Tutorial guide](https://doc.zeugwerk.dev/release/1.6/framework/tutorials/quickstart.html).

## How it works

The visualization communicates with the PLC exclusively through TwinCAT ADS. Generated C# bindings in `bindings/Tutorial_Quickstart/` map PLC struct types (`QuickstartCom`, `ZApplication.AlarmingCom`) to strongly typed C# classes consumed directly by the Godot scene.

## Bindings

The `bindings/` folder contains type-safe C# wrappers generated from the PLC's COM interface via [zkbindings](https://zeugwerk.at/products/devtools/#zkbindings---binding-generation-for-c-and-c). They map PLC struct types (`QuickstartCom`, `ZApplication.AlarmingCom`) to C# classes so the Godot scene can interact with PLC symbols without string-based ADS lookups. The bindings in this repo are kept in sync with the [Tutorial-Quickstart](https://github.com/Zeugwerk/Tutorial-Quickstart) PLC project.

## Links

- [Quickstart Tutorial guide](https://doc.zeugwerk.dev/release/1.6/framework/tutorials/quickstart.html)
- [Zeugwerk Framework documentation](https://doc.zeugwerk.dev/release/1.6/framework/overview.html)
- [Tutorial-Quickstart PLC project](https://github.com/Zeugwerk/Tutorial-Quickstart)
- [zkbindings](https://zeugwerk.at/products/devtools/#zkbindings---binding-generation-for-c-and-c)
- [Zeugwerk Creator MCP demo](https://zeugwerk.at/blog/creator-1-8-cli-mcp/)
- [Zeugwerk website](https://zeugwerk.at)
