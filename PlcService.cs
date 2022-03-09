using Godot;
using System;
using TwinCAT.Ads;
using System.Collections.Generic;
using System.Linq;

public class PlcService : Node
{
	TcAdsClient _ads = null;
	PLC.Mirror.QuickstartCom _quickstart = null;
	PLC.Mirror.ZApplication_AlarmingCom _alarming = null;

	float _dt;
	PLC.Enums.ZApplication_UnitStateMachineState _stateMem;
	Node _magnetArea;
	RigidBody _cube;
	Node _gripper;
	bool _magnetIsOn, _inMagnetZone;
	bool _pressed;
	bool _showException = true;

	private void ConnectPlc()
	{
		var netId = GetNode<LineEdit>("../MessageLayer/ErrorRect/leAmsNetId").Text;
		GD.Print($"Trying to connect to PLC {netId}");
		
		try
		{
			_ads = new TcAdsClient();

			if(netId == "")
				_ads.Connect(851);
			else
				_ads.Connect(netId, 851);

			_quickstart = new PLC.Mirror.QuickstartCom("ZGlobal.Com.Unit.Quickstart", _ads);
			_alarming = new PLC.Mirror.ZApplication_AlarmingCom("ZGlobal.Com.Alarming", _ads);
			_quickstart.Subscribe.Equipment.LimitSwitchLeft.Sync = new PLC.Types.ZApplication_DigitalComSubscribe { Enable = 0, Write = 1 };
			_quickstart.Subscribe.Equipment.LimitSwitchRight.Sync = new PLC.Types.ZApplication_DigitalComSubscribe { Enable = 0, Write = 1 };

			GetNode<ColorRect>("../MessageLayer/ErrorRect").Visible = false;
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}

	private void DisconnectPlc(Exception ex)
	{
		// create dummy classes
		if(_ads != null)
		{
			_quickstart = new PLC.Mirror.QuickstartCom("ZGlobal.Com.Unit.Quickstart");
			_alarming = new PLC.Mirror.ZApplication_AlarmingCom("ZGlobal.Com.Alarming");		
		}
		_ads = null;

		GetNode<ColorRect>("../MessageLayer/ErrorRect").Visible = _showException;
		GetNode<Label>("../MessageLayer/ErrorRect/lblConnectionState").Text =
			$@"Not connected to local PLC
This application is designed to interface with the Zeugwerk Quickstart Tutorial.";
	}

	// Called when the node enters the scene tree for the first time.git
	public override void _Ready()
	{
		ConnectPlc();

		_cube = GetNode<RigidBody>("CubeRigidBody");
		_magnetArea = GetNode("bxRail/bxTransportX/rbCylinderY").GetNode("MagnetArea");
		_gripper = GetNode("bxRail/bxTransportX/rbCylinderY/Gripper");

		// Reset Limit switches
		_on_Right_area_exited(_cube.GetNode<Area>("Area"));
		_on_Left_area_exited(_cube.GetNode<Area>("Area"));
		_on_IsDownLs_area_exited(GetNode<Area>("bxRail/bxTransportX/rbCylinderY/GripperUpper/Area"));
		_on_IsUpLs_area_exited(GetNode<Area>("bxRail/bxTransportX/rbCylinderY/Gripper/Area"));		
		
		GetNode("bxRail/bxTransportX/rbCylinderY/MagnetArea").Connect("area_entered", this, nameof(_enteredMagnet));
		GetNode("bxRail/bxTransportX/rbCylinderY/MagnetArea").Connect("area_exited", this, nameof(_exitedMagnet));
		GetNode("stConveyor/Area").Connect("area_exited", this, nameof(_resetCube));
		
		GetNode("Right").Connect("area_entered", this, nameof(_on_Right_area_entered));
		GetNode("Right").Connect("area_exited", this, nameof(_on_Right_area_exited));
		GetNode("Left").Connect("area_entered", this, nameof(_on_Left_area_entered));
		GetNode("Left").Connect("area_exited", this, nameof(_on_Left_area_exited));
		
		GetNode("bxRail/bxTransportX/IsDownLs/Area").Connect("area_entered", this, nameof(_on_IsDownLs_area_entered));
		GetNode("bxRail/bxTransportX/IsDownLs/Area").Connect("area_exited", this, nameof(_on_IsDownLs_area_exited));
		GetNode("bxRail/bxTransportX/IsUpLs/Area").Connect("area_entered", this, nameof(_on_IsUpLs_area_entered));
		GetNode("bxRail/bxTransportX/IsUpLs/Area").Connect("area_exited", this, nameof(_on_IsUpLs_area_exited));		
		
		GetNode("../MessageLayer/ErrorRect/btIgnore").Connect("pressed", this, nameof(_ignoreError));
		GetNode("../MessageLayer/ErrorRect/btReconnect").Connect("pressed", this, nameof(_reconnect));		
		
		GetNode("../GUI/Sequences/Start").Connect("pressed", this, nameof(_start));
		GetNode("../GUI/Sequences/Stop").Connect("pressed", this, nameof(_stop));
		GetNode("../GUI/Sequences/GoHome").Connect("pressed", this, nameof(_gohome));
		GetNode("../GUI/Equipment/MoveUp").Connect("pressed", this, nameof(_moveup));
		GetNode("../GUI/Equipment/MoveDown").Connect("pressed", this, nameof(_movedown));
		GetNode("../GUI/Equipment/JogLeft").Connect("pressed", this, nameof(_jogTransportXLeft));
		GetNode("../GUI/Equipment/JogLeft").Connect("button_up", this, nameof(_stopTransport));
		GetNode("../GUI/Equipment/JogRight").Connect("pressed", this, nameof(_jogTransportXRight));
		GetNode("../GUI/Equipment/JogRight").Connect("button_up", this, nameof(_stopTransport));
		GetNode("../GUI/Equipment/MagnetOn").Connect("pressed", this, nameof(_magnetOn));
		GetNode("../GUI/Equipment/MagnetOff").Connect("pressed", this, nameof(_magnetOff));
		GetNode("../GUI/Equipment/ConveyorOn").Connect("pressed", this, nameof(_conveyorOn));
		GetNode("../GUI/Equipment/ConveyorOff").Connect("pressed", this, nameof(_conveyorOff));
		GetNode("../GUI/Game/ResetCube").Connect("pressed", this, nameof(_resetCube_pressed));
		GetNode("../GUI/Game/ToggleOverlay").Connect("pressed", this, nameof(_toggleOverlay));
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(float delta)
	{
		GetNode<ColorRect>("../Overlay/ColorRect/crLeft").RectPosition = GetNode<Camera>("../ClippedCamera").UnprojectPosition(GetNode<Area>("Left").GlobalTransform.origin);
		GetNode<ColorRect>("../Overlay/ColorRect/crRight").RectPosition = GetNode<Camera>("../ClippedCamera").UnprojectPosition(GetNode<Area>("Right").GlobalTransform.origin);
		GetNode<ColorRect>("../Overlay/ColorRect/crConveyor").RectPosition = GetNode<Camera>("../ClippedCamera").UnprojectPosition(GetNode<StaticBody>("stConveyor").GlobalTransform.origin);
		GetNode<ColorRect>("../Overlay/ColorRect/crTransport").RectPosition = GetNode<Camera>("../ClippedCamera").UnprojectPosition(GetNode<CSGBox>("bxRail/bxTransportX").GlobalTransform.origin);
		GetNode<ColorRect>("../Overlay/ColorRect/crMagnet").RectPosition = GetNode<Camera>("../ClippedCamera").UnprojectPosition(GetNode<CSGBox>("bxRail/bxTransportX/rbCylinderY/Gripper").GlobalTransform.origin);
		GetNode<ColorRect>("../Overlay/ColorRect/crLeft").RectPosition = new Vector2(GetNode<ColorRect>("../Overlay/ColorRect/crLeft").RectPosition.x - 120, GetNode<ColorRect>("../Overlay/ColorRect/crLeft").RectPosition.y+30);
		GetNode<ColorRect>("../Overlay/ColorRect/crRight").RectPosition = new Vector2(GetNode<ColorRect>("../Overlay/ColorRect/crRight").RectPosition.x, GetNode<ColorRect>("../Overlay/ColorRect/crRight").RectPosition.y+30);
		GetNode<ColorRect>("../Overlay/ColorRect/crConveyor").RectPosition = new Vector2(GetNode<ColorRect>("../Overlay/ColorRect/crConveyor").RectPosition.x - 60, GetNode<ColorRect>("../Overlay/ColorRect/crConveyor").RectPosition.y+30);
		GetNode<ColorRect>("../Overlay/ColorRect/crTransport").RectPosition = new Vector2(GetNode<ColorRect>("../Overlay/ColorRect/crTransport").RectPosition.x - 60, GetNode<ColorRect>("../Overlay/ColorRect/crTransport").RectPosition.y-60);
		GetNode<ColorRect>("../Overlay/ColorRect/crMagnet").RectPosition = new Vector2(GetNode<ColorRect>("../Overlay/ColorRect/crMagnet").RectPosition.x - 60, GetNode<ColorRect>("../Overlay/ColorRect/crMagnet").RectPosition.y-30);

		if (_ads == null)
			return;
				
		// Read data from PLC
		PLC.Types.QuickstartComPublish status;
		PLC.Types.ZApplication_AlarmingComPublish alarming;
		try
		{
			status = _quickstart.Publish.Sync;
			alarming = _alarming.Publish.Sync;
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
			return;
		}

		// Alarming
		var message = alarming.Buffer.data.Where(x => x.Extend.LogLevel > PLC.Enums.ZCore_LogLevel.Debug).OrderBy(x => x.Extend.LogLevel).FirstOrDefault();
		if(message.State != PLC.Enums.ZApplication_AlarmingState.Undefined)
			GetNode<Label>("../GUI/lbMessage").Text = $"[{(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds(message.Extend.TimeStamp / 1000).ToLocalTime()}]\n[{message.State}] {message.Extend.Text.str}";
		else
			GetNode<Label>("../GUI/lbMessage").Text = "";
			
		// Update equipment state
		StaticBody conveyorStaticBody = GetNode<StaticBody>("stConveyor");
		if(conveyorStaticBody.ConstantLinearVelocity.x > 0)
		{
			var mat = conveyorStaticBody.GetNode<CSGBox>("CSGBox").Material as SpatialMaterial;
			mat.Uv1Offset = new Vector3(0, mat.Uv1Offset.y + (1.5f * delta), 0);
		}

		// throttle plc read/write access
		_dt += delta;
		if (_dt > 0.03)
		{
			GetNode<Button>("../GUI/Sequences/Start").Disabled = status.Request.Start == 0;
			GetNode<Button>("../GUI/Sequences/GoHome").Disabled = status.Request.GoHome == 0;
			
			var colorOff = new Color(202.0f/255.0f,208.0f/255.0f,222.0f/255.0f);
			var colorOn = new Color(218.0f/255.0f,119.0f/255.0f,109.0f/255.0f);			
			GetNode<Label>("../GUI/Equipment/lbPosition").Text = string.Format("Position: {0:0.000}", status.Equipment.TransportX.Base.ActualPosition);
			GetNode<ColorRect>("../GUI/Equipment/crTransport").Color = status.Equipment.TransportX.Base.IsDriveEnabled > 0 ? colorOn : colorOff;
			GetNode<ColorRect>("../GUI/Equipment/crMagnet").Color = status.Equipment.MagnetOn.IsEnabled > 0 ? colorOn : colorOff;
			GetNode<ColorRect>("../GUI/Equipment/crConveyor").Color = status.Equipment.ConveyorOn.IsEnabled > 0 ? colorOn : colorOff;
			
			GetNode("stConveyor/Area").SetBlockSignals(status.State == PLC.Enums.ZApplication_UnitStateMachineState.GoHome);
			if (status.State == PLC.Enums.ZApplication_UnitStateMachineState.Idle && _stateMem == PLC.Enums.ZApplication_UnitStateMachineState.GoHome)
				_resetCube();

			var label = GetNode<Label>("../GUI/lblUnitState");
			label.Text = status.State.ToString();

			// switch conveyor velocity on/off
			conveyorStaticBody.ConstantLinearVelocity = new Vector3(status.Equipment.ConveyorOn.IsEnabled > 0 ? 1.5f : 0.0f, 0.0f, 0.0f);
			_dt = 0;

			// transport axis
			CSGBox transportX = GetNode("bxRail").GetNode<CSGBox>("bxTransportX");
			transportX.Translation = new Vector3((float)status.Equipment.TransportX.Base.ActualPosition, transportX.Translation.y, transportX.Translation.z);

			// vertical cylinder
			var cylinderY = transportX.GetNode<RigidBody>("rbCylinderY");

			if (status.Equipment.CylinderYUp.IsEnabled == 0 && status.Equipment.CylinderYDown.IsEnabled > 0)
				cylinderY.GravityScale = 2;
			else if (status.Equipment.CylinderYUp.IsEnabled > 0 && status.Equipment.CylinderYDown.IsEnabled == 0)
				cylinderY.GravityScale = -2;

			// magnetic force on/off
			_magnetIsOn = status.Equipment.MagnetOn.IsEnabled > 0;
			GetNode("stConveyor/Area").SetBlockSignals(true);
			GetNode("Right").SetBlockSignals(true);
			GetNode("Left").SetBlockSignals(true);
			GetNode("Left").SetBlockSignals(true);
			_magnetArea.SetBlockSignals(true);
			if (_magnetIsOn)
			{
				((SpatialMaterial)((CSGBox)_gripper).Material).EmissionEnabled = true;

				if (_inMagnetZone)
				{
					if (_cube.Mode != RigidBody.ModeEnum.Static)
					{
						_cube.Mode = RigidBody.ModeEnum.Static;
						cylinderY.CollisionMask = 0x1;

						var cubeHeight = 0.5f;
						var oldGlobal = _cube.GlobalTransform;
						_cube.GetParent().RemoveChild(_cube);
						_gripper.AddChild(_cube);
						_cube.Translation = new Vector3(0, -0.5f * (cubeHeight + ((CSGBox)(_gripper)).Height), 0);
						_cube.Rotation = new Vector3(0, 0, 0);

						// Set Collision Geometry to gripper + cube
						GetNode<CollisionShape>("bxRail/bxTransportX/rbCylinderY/GripperCollisionShape").Translation 
							= new Vector3(((CSGBox)(_gripper)).Translation.x, ((CSGBox)(_gripper)).Translation.y - 0.5f * cubeHeight, ((CSGBox)(_gripper)).Translation.z);
						((BoxShape)(GetNode<CollisionShape>("bxRail/bxTransportX/rbCylinderY/GripperCollisionShape").Shape)).Extents =
							new Vector3(((CSGBox)(_gripper)).Width / 2, 0.5f * (((CSGBox)(_gripper)).Height + cubeHeight), ((CSGBox)(_gripper)).Depth / 2);
					}
				}
			}
			else
			{
				((SpatialMaterial)((CSGBox)_gripper).Material).EmissionEnabled = false;		

				if (_cube.Mode != RigidBody.ModeEnum.Rigid)
				{
					_cube.Mode = RigidBody.ModeEnum.Rigid;
					cylinderY.CollisionMask = 0x3;

					var transform = _cube.GlobalTransform;
					_cube.GetParent().RemoveChild(_cube);
					AddChild(_cube);
					_cube.AddCentralForce(new Vector3(0, 0, 0));
					_cube.Transform = transform;
					
					// Set Collision Geometry to gripper
					GetNode<CollisionShape>("bxRail/bxTransportX/rbCylinderY/GripperCollisionShape").Translation = ((CSGBox)(_gripper)).Translation;
					((BoxShape)(GetNode<CollisionShape>("bxRail/bxTransportX/rbCylinderY/GripperCollisionShape")).Shape).Extents =
						new Vector3(0.25f, 0.025f, 0.25f);
				}
			}
			
			GetNode("stConveyor/Area").SetBlockSignals(false);
			GetNode("Right").SetBlockSignals(false);
			GetNode("Left").SetBlockSignals(false);
			GetNode("Left").SetBlockSignals(false);
			_magnetArea.SetBlockSignals(false);

			_stateMem = status.State;
		}
	}
	
	public void _resetCube_pressed()
	{
		_resetCube(null);
	}

	public void _resetCube(Area area=null)
	{
		if (area != null && area != _cube.GetNode("Area"))
			return;

		//if (_cube.Mode != RigidBody.ModeEnum.Rigid)
		//{
			_cube.GetParent().RemoveChild(_cube);
			AddChild(_cube);
			_cube.Mode = RigidBody.ModeEnum.Rigid;
			_cube.AddCentralForce(new Vector3(0, 0, 0));
		//}

		_cube.AngularVelocity = new Vector3(0, 0, 0);
		_cube.LinearVelocity = new Vector3(0, 0, 0);

		Random rnd = new Random();
		_cube.Translation =
			new Vector3((float)(-3 + rnd.NextDouble() * 0.1), (float)(0.7 + rnd.NextDouble() * 0.01), (float)(-0.1 + rnd.NextDouble() * 0.2));

		_cube.Rotation =
			new Vector3((float)(-2 + rnd.NextDouble() * 0.004), (float)(-2 + rnd.NextDouble() * 0.004), (float)(-1 + rnd.NextDouble() * 0.004));
	}
	
	public void ToggleAreaBox(CSGBox box, bool on, PLC.Mirror.ZApplication_DigitalComSubscribe digitalIo)
	{
		((SpatialMaterial)box.Material).EmissionEnabled = on;
		
		if(on)
			((SpatialMaterial)box.Material).AlbedoColor = new Color(1,0,0,0.5f);
		else
			((SpatialMaterial)box.Material).AlbedoColor = new Color(0,1,0,0.5f);		

		try
		{
			digitalIo.Sync = new PLC.Types.ZApplication_DigitalComSubscribe { Enable = on ? (byte)1 : (byte)0, Write = 1 };
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}		
	}

	public void _on_Right_area_entered(Area area)
	{
		if (area != _cube.GetNode("Area")) return;

		ToggleAreaBox(GetNode<CSGBox>("Right/CollisionShape/RightLimitSwitch"), on: true, digitalIo: _quickstart.Subscribe.Equipment.LimitSwitchRight );
	}

	public void _on_Right_area_exited(Area area=null)
	{
		if (area != _cube.GetNode("Area")) return;
		ToggleAreaBox(GetNode<CSGBox>("Right/CollisionShape/RightLimitSwitch"), on: false, digitalIo: _quickstart.Subscribe.Equipment.LimitSwitchRight);
	}

	public void _on_Left_area_entered(Area area)
	{
		if (area != _cube.GetNode("Area")) return;
		ToggleAreaBox(GetNode<CSGBox>("Left/CollisionShape/LeftLimitSwitch"), on: true, digitalIo: _quickstart.Subscribe.Equipment.LimitSwitchLeft);
	}

	public void _on_Left_area_exited(Area area)
	{
		if (area != _cube.GetNode("Area")) return;
		ToggleAreaBox(GetNode<CSGBox>("Left/CollisionShape/LeftLimitSwitch"), on: false, digitalIo: _quickstart.Subscribe.Equipment.LimitSwitchLeft);
	}

	public void _on_IsDownLs_area_entered(Area area)
	{
		if (area != GetNode("bxRail/bxTransportX/rbCylinderY/GripperUpper/Area")) return;	
		ToggleAreaBox(GetNode<CSGBox>("bxRail/bxTransportX/IsDownLs"), on: true, digitalIo: _quickstart.Subscribe.Equipment.CylinderYIsDown);
	}

	public void _on_IsDownLs_area_exited(Area area)
	{
		if (area != GetNode("bxRail/bxTransportX/rbCylinderY/GripperUpper/Area")) return;		
		ToggleAreaBox(GetNode<CSGBox>("bxRail/bxTransportX/IsDownLs"), on: false, digitalIo: _quickstart.Subscribe.Equipment.CylinderYIsDown);
	}
	
	public void _on_IsUpLs_area_entered(Area area)
	{
		if (area != GetNode("bxRail/bxTransportX/rbCylinderY/Gripper/Area")) return;
		ToggleAreaBox(GetNode<CSGBox>("bxRail/bxTransportX/IsUpLs"), on: true, digitalIo: _quickstart.Subscribe.Equipment.CylinderYIsUp);
	}

	public void _on_IsUpLs_area_exited(Area area)
	{
		if (area != GetNode("bxRail/bxTransportX/rbCylinderY/Gripper/Area")) return;	
		ToggleAreaBox(GetNode<CSGBox>("bxRail/bxTransportX/IsUpLs"), on: false, digitalIo: _quickstart.Subscribe.Equipment.CylinderYIsUp);
	}

	public void _moveup()
	{
		try
		{
			_quickstart.Subscribe.Equipment.CylinderY.Sync = new PLC.Types.ZApplication_ActuatorDigitalComSubscribe { MovePlus = 1 };
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}

	public void _movedown()
	{
		try
		{
			_quickstart.Subscribe.Equipment.CylinderY.Sync = new PLC.Types.ZApplication_ActuatorDigitalComSubscribe { MoveMinus = 1 };
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}

	public void _magnetOn()
	{
		try
		{
			_quickstart.Subscribe.Equipment.MagnetOn.Sync = new PLC.Types.ZApplication_DigitalComSubscribe { Enable = 1, Write = 1 };
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}
	public void _magnetOff()
	{
		try
		{
			_quickstart.Subscribe.Equipment.MagnetOn.Sync = new PLC.Types.ZApplication_DigitalComSubscribe { Enable = 0, Write = 1 };
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}

	public void _conveyorOn()
	{
		try
		{
			_quickstart.Subscribe.Equipment.ConveyorOn.Sync = new PLC.Types.ZApplication_DigitalComSubscribe { Enable = 1, Write = 1 };
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}
	public void _conveyorOff()
	{
		try
		{
			_quickstart.Subscribe.Equipment.ConveyorOn.Sync = new PLC.Types.ZApplication_DigitalComSubscribe { Enable = 0, Write = 1 };
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}

	public void _jogTransportXLeft()
	{
		if (!_pressed)
		{
			_pressed = true;
			try
			{
				_quickstart.Subscribe.Equipment.TransportX.Position.Sync = new PLC.Types.ZApplication_AxisComPositionSubscribe { Position1 = -3.5, Speed = 0.7, MoveAbsolute1 = 1 };
			}
			catch (Exception ex)
			{
				DisconnectPlc(ex);
			}
		}
	}
	public void _jogTransportXRight()
	{
		if (!_pressed)
		{
			_pressed = true;
			try
			{
				_quickstart.Subscribe.Equipment.TransportX.Position.Sync = new PLC.Types.ZApplication_AxisComPositionSubscribe { Position1 = 3.5, Speed = 0.7, MoveAbsolute1 = 1 };
			}
			catch (Exception ex)
			{
				DisconnectPlc(ex);
			}
		}
	}
	public void _stopTransport()
	{
		if (_pressed)
		{
			_pressed = false;
			try
			{
				_quickstart.Subscribe.Equipment.TransportX.Base.Sync = new PLC.Types.ZApplication_AxisComBaseSubscribe { Stop = 1 };
			}
			catch (Exception ex)
			{
				DisconnectPlc(ex);
			}
		}
	}

	public void _stop()
	{
		try
		{
			_quickstart.Subscribe.Request.Sync = new PLC.Types.QuickstartComRequest { Stop = 1 };
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}

	public void _start()
	{
		try
		{
			_quickstart.Subscribe.Request.Sync = new PLC.Types.QuickstartComRequest { Start = 1 };
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}

	public void _gohome()
	{
		try
		{
			_quickstart.Subscribe.Request.Sync = new PLC.Types.QuickstartComRequest { GoHome = 1 };
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}
	
	public void _enteredMagnet(Area area)
	{
		if (area != _cube.GetNode("Area"))
			return;

		_inMagnetZone = true;
	}

	public void _exitedMagnet(Area area)
	{
		if (area != _cube.GetNode("Area"))
			return;

		_inMagnetZone = false;
	}
	
	public void _ignoreError()
	{
		_showException = false;
		GetNode<ColorRect>("../MessageLayer/ErrorRect").Visible = _showException;
	}

	public void _reconnect()
	{
		ConnectPlc();
	}

	public void _toggleOverlay()
	{
		GetNode<ColorRect>("../Overlay/ColorRect").Visible = !GetNode<ColorRect>("../Overlay/ColorRect").Visible;
	}	
}
