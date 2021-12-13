using Godot;
using System;
using TwinCAT.Ads;
using System.Collections.Generic;
using System.Linq;

public class PlcService : Node
{
	TcAdsClient _ads = null;
	ITcAdsSymbol _statusSymbol, _alarmingSymbol;
	ITcAdsSymbol _controlLeftLimitSwitch, _controlRightLimitSwitch;
	ITcAdsSymbol _controlTransportXPosition, _controlTransportXBase;
	ITcAdsSymbol _controlMagnetOn, _controlConveyorOn;
	ITcAdsSymbol _controlCylinderY;
	ITcAdsSymbol _controlIsDownLs, _controlIsUpLs;	
	ITcAdsSymbol _controlRequest;

	float _dt;
	PLC.Enums.ZApplication_UnitStateMachineState _stateMem;
	Node _magnetArea;
	RigidBody _cube;
	Node _gripper;
	bool _magnetIsOn, _inMagnetZone;
	bool _pressed;
	float _disconnectedDt;
	bool _showException = true;

	private void ConnectPlc()
	{
		try
		{
			_disconnectedDt = 0;
			
			_ads = new TcAdsClient();
			_ads.Connect(851);
			_alarmingSymbol = _ads.ReadSymbolInfo("ZGlobal.Com.Alarming.publish");
			_statusSymbol = _ads.ReadSymbolInfo("ZGlobal.Com.Unit.UselessBox.publish");
			_controlRequest = _ads.ReadSymbolInfo("ZGlobal.Com.Unit.UselessBox.Subscribe.Request");
			_controlLeftLimitSwitch = _ads.ReadSymbolInfo("ZGlobal.Com.Unit.UselessBox.Subscribe.Equipment.LimitSwitchLeft");
			_controlRightLimitSwitch = _ads.ReadSymbolInfo("ZGlobal.Com.Unit.UselessBox.Subscribe.Equipment.LimitSwitchRight");
			_controlIsDownLs = _ads.ReadSymbolInfo("ZGlobal.Com.Unit.UselessBox.Subscribe.Equipment.CylinderYIsDown");
			_controlIsUpLs = _ads.ReadSymbolInfo("ZGlobal.Com.Unit.UselessBox.Subscribe.Equipment.CylinderYIsUp");
			_controlCylinderY = _ads.ReadSymbolInfo("ZGlobal.Com.Unit.UselessBox.Subscribe.Equipment.CylinderY");
			_controlTransportXPosition = _ads.ReadSymbolInfo("ZGlobal.Com.Unit.UselessBox.Subscribe.Equipment.TransportX.Position");
			_controlTransportXBase = _ads.ReadSymbolInfo("ZGlobal.Com.Unit.UselessBox.Subscribe.Equipment.TransportX.Base");
			_controlMagnetOn = _ads.ReadSymbolInfo("ZGlobal.Com.Unit.UselessBox.Subscribe.Equipment.MagnetOn");
			_controlConveyorOn = _ads.ReadSymbolInfo("ZGlobal.Com.Unit.UselessBox.Subscribe.Equipment.ConveyorOn");

			_ads.WriteAny((uint)_controlLeftLimitSwitch.IndexGroup, (uint)_controlLeftLimitSwitch.IndexOffset, new PLC.Types.ZApplication_DigitalComSubscribe { Enable = 0, Write = 1 });
			_ads.WriteAny((uint)_controlRightLimitSwitch.IndexGroup, (uint)_controlRightLimitSwitch.IndexOffset, new PLC.Types.ZApplication_DigitalComSubscribe { Enable = 0, Write = 1 });
			
			GetNode<ColorRect>("../MessageLayer/ErrorRect").Visible = false;
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}

	private void DisconnectPlc(Exception ex)
	{
		_alarmingSymbol = null;
		_statusSymbol = null;
		_controlRequest = null;
		_controlLeftLimitSwitch = null;
		_controlRightLimitSwitch = null;
		_controlIsDownLs = null;
		_controlIsUpLs = null;		
		_controlCylinderY = null;
		_controlTransportXPosition = null;
		_controlTransportXBase = null;
		_controlMagnetOn = null;
		_controlConveyorOn = null;
		_ads = null;

		GetNode<ColorRect>("../MessageLayer/ErrorRect").Visible = _showException;
		GetNode<Label>("../MessageLayer/ErrorRect/lblConnectionState").Text =
			$@"Not connected to local PLC
This application is designed to interface with the Zeugwerk Quickstart Tutorial.

{ex?.ToString()}";
	}

	// Called when the node enters the scene tree for the first time.git
	public override void _Ready()
	{
		ConnectPlc();

		_cube = GetNode<RigidBody>("CubeRigidBody");
		_magnetArea = GetNode("Rail/TransportX/CylinderYRigidBody").GetNode("MagnetArea");
		_gripper = GetNode("Rail/TransportX/CylinderYRigidBody/Gripper");

		// Reset Limit switches
		_on_Right_area_exited(_cube.GetNode<Area>("Area"));
		_on_Left_area_exited(_cube.GetNode<Area>("Area"));
		_on_IsDownLs_area_exited(GetNode<Area>("Rail/TransportX/CylinderYRigidBody/GripperUpper/Area"));
		_on_IsUpLs_area_exited(GetNode<Area>("Rail/TransportX/CylinderYRigidBody/Gripper/Area"));		
		
		GetNode("Rail/TransportX/CylinderYRigidBody/MagnetArea").Connect("area_entered", this, nameof(_enteredMagnet));
		GetNode("Rail/TransportX/CylinderYRigidBody/MagnetArea").Connect("area_exited", this, nameof(_exitedMagnet));
		GetNode("ConveyorStaticBody/Area").Connect("area_exited", this, nameof(_resetCube));
		
		GetNode("Right").Connect("area_entered", this, nameof(_on_Right_area_entered));
		GetNode("Right").Connect("area_exited", this, nameof(_on_Right_area_exited));
		GetNode("Left").Connect("area_entered", this, nameof(_on_Left_area_entered));
		GetNode("Left").Connect("area_exited", this, nameof(_on_Left_area_exited));
		
		GetNode("Rail/TransportX/IsDownLs/Area").Connect("area_entered", this, nameof(_on_IsDownLs_area_entered));
		GetNode("Rail/TransportX/IsDownLs/Area").Connect("area_exited", this, nameof(_on_IsDownLs_area_exited));
		GetNode("Rail/TransportX/IsUpLs/Area").Connect("area_entered", this, nameof(_on_IsUpLs_area_entered));
		GetNode("Rail/TransportX/IsUpLs/Area").Connect("area_exited", this, nameof(_on_IsUpLs_area_exited));		
		
		GetNode("../MessageLayer/ErrorRect/btIgnore").Connect("pressed", this, nameof(_ignoreError));
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
	}

	//  // Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(float delta)
	{
		if (_ads == null)
		{
			GetNode<Label>("../MessageLayer/ErrorRect/lbReconnect").Text = string.Format("Reconnecting in {0}s", (int)(5-_disconnectedDt));

			_disconnectedDt += delta;
			if(_disconnectedDt > 5)
				ConnectPlc();
			return;
		}
				
		// Read data from PLC
		PLC.Types.UselessBoxComPublish status;
		PLC.Types.ZApplication_AlarmingComPublish alarming;
		try
		{
			status = _ads.ReadSymbol<PLC.Types.UselessBoxComPublish>(_statusSymbol);
			alarming = _ads.ReadSymbol<PLC.Types.ZApplication_AlarmingComPublish>(_alarmingSymbol);
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
			return;
		}

		// Alarming
		var message = alarming.Buffer.Where(x => x.Extend.LogLevel > PLC.Enums.ZCore_LogLevel.Debug).OrderBy(x => x.Extend.LogLevel).FirstOrDefault();
		if(message.State != PLC.Enums.ZApplication_AlarmingState.Undefined)
			GetNode<Label>("../GUI/lbMessage").Text = $"[{(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds(message.Extend.TimeStamp / 1000).ToLocalTime()}]\n[{message.State}] {message.Extend.Text.str}";
		else
			GetNode<Label>("../GUI/lbMessage").Text = "";
			
		// Update equipment state
		StaticBody conveyorStaticBody = GetNode<StaticBody>("ConveyorStaticBody");
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
			GetNode<Button>("../GUI/Sequences/Stop").Disabled = status.Request.Stop == 0;
			GetNode<Button>("../GUI/Sequences/GoHome").Disabled = status.Request.Gohome == 0;
			
			var colorOff = new Color(202.0f/255.0f,208.0f/255.0f,222.0f/255.0f);
			var colorOn = new Color(218.0f/255.0f,119.0f/255.0f,109.0f/255.0f);			
			GetNode<Label>("../GUI/Equipment/lbPosition").Text = string.Format("Position: {0:0.000}", status.Equipment.TransportX.Position.ActPosition);
			GetNode<ColorRect>("../GUI/Equipment/crTransport").Color = status.Equipment.TransportX.Base.IsDriveEnabled > 0 ? colorOn : colorOff;
			GetNode<ColorRect>("../GUI/Equipment/crMagnet").Color = status.Equipment.MagnetOn.IsEnabled > 0 ? colorOn : colorOff;
			GetNode<ColorRect>("../GUI/Equipment/crConveyor").Color = status.Equipment.ConveyorOn.IsEnabled > 0 ? colorOn : colorOff;
			
			GetNode("ConveyorStaticBody/Area").SetBlockSignals(status.State == PLC.Enums.ZApplication_UnitStateMachineState.Gohome);
			if (status.State == PLC.Enums.ZApplication_UnitStateMachineState.Idle && _stateMem == PLC.Enums.ZApplication_UnitStateMachineState.Gohome)
				_resetCube();

			var label = GetNode<Label>("../GUI/lblUnitState");
			label.Text = status.State.ToString();

			// switch conveyor velocity on/off
			conveyorStaticBody.ConstantLinearVelocity = new Vector3(status.Equipment.ConveyorOn.IsEnabled > 0 ? 1.5f : 0.0f, 0.0f, 0.0f);
			_dt = 0;

			// transport axis
			CSGBox transportX = GetNode("Rail").GetNode<CSGBox>("TransportX");
			transportX.Translation = new Vector3((float)status.Equipment.TransportX.Position.ActPosition, transportX.Translation.y, transportX.Translation.z);

			// vertical cylinder
			var cylinderY = transportX.GetNode<RigidBody>("CylinderYRigidBody");

			if (status.Equipment.CylinderYMoveUp.IsEnabled == 0 && status.Equipment.CylinderYMoveDown.IsEnabled > 0)
				cylinderY.GravityScale = 2;
			else if (status.Equipment.CylinderYMoveUp.IsEnabled > 0 && status.Equipment.CylinderYMoveDown.IsEnabled == 0)
				cylinderY.GravityScale = -2;

			// magnetic force on/off
			_magnetIsOn = status.Equipment.MagnetOn.IsEnabled > 0;
			GetNode("ConveyorStaticBody/Area").SetBlockSignals(true);
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
						GetNode<CollisionShape>("Rail/TransportX/CylinderYRigidBody/GripperCollisionShape").Translation 
							= new Vector3(((CSGBox)(_gripper)).Translation.x, ((CSGBox)(_gripper)).Translation.y - 0.5f * cubeHeight, ((CSGBox)(_gripper)).Translation.z);
						((BoxShape)(GetNode<CollisionShape>("Rail/TransportX/CylinderYRigidBody/GripperCollisionShape").Shape)).Extents =
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
					GetNode<CollisionShape>("Rail/TransportX/CylinderYRigidBody/GripperCollisionShape").Translation = ((CSGBox)(_gripper)).Translation;
					((BoxShape)(GetNode<CollisionShape>("Rail/TransportX/CylinderYRigidBody/GripperCollisionShape")).Shape).Extents =
						new Vector3(0.25f, 0.025f, 0.25f);
				}
			}
			
			GetNode("ConveyorStaticBody/Area").SetBlockSignals(false);
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
			GD.Print("changing cube collision mode");
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
	
	public void ToggleAreaBox(CSGBox box, bool on, ITcAdsSymbol sym)
	{
		((SpatialMaterial)box.Material).EmissionEnabled = on;
		
		if(on)
			((SpatialMaterial)box.Material).AlbedoColor = new Color(1,0,0,0.5f);
		else
			((SpatialMaterial)box.Material).AlbedoColor = new Color(0,1,0,0.5f);		

		try
		{
			_ads?.WriteAny((uint)sym.IndexGroup, (uint)sym.IndexOffset, new PLC.Types.ZApplication_DigitalComSubscribe { Enable = on ? (byte)1 : (byte)0, Write = 1 });
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}		
	}

	public void _on_Right_area_entered(Area area)
	{
		if (area != _cube.GetNode("Area")) return;
		ToggleAreaBox(GetNode<CSGBox>("Right/CollisionShape/RightLimitSwitch"), on: true, sym: _controlRightLimitSwitch);
	}

	public void _on_Right_area_exited(Area area=null)
	{
		if (area != _cube.GetNode("Area")) return;
		ToggleAreaBox(GetNode<CSGBox>("Right/CollisionShape/RightLimitSwitch"), on: false, sym: _controlRightLimitSwitch);
	}

	public void _on_Left_area_entered(Area area)
	{
		if (area != _cube.GetNode("Area")) return;
		ToggleAreaBox(GetNode<CSGBox>("Left/CollisionShape/LeftLimitSwitch"), on: true, sym: _controlLeftLimitSwitch);
	}

	public void _on_Left_area_exited(Area area)
	{
		if (area != _cube.GetNode("Area")) return;
		ToggleAreaBox(GetNode<CSGBox>("Left/CollisionShape/LeftLimitSwitch"), on: false, sym: _controlLeftLimitSwitch);
	}

	public void _on_IsDownLs_area_entered(Area area)
	{
		if (area != GetNode("Rail/TransportX/CylinderYRigidBody/GripperUpper/Area")) return;	
		
		GD.Print("is down entered")	;
		ToggleAreaBox(GetNode<CSGBox>("Rail/TransportX/IsDownLs"), on: true, sym: _controlIsDownLs);
	}

	public void _on_IsDownLs_area_exited(Area area)
	{
		if (area != GetNode("Rail/TransportX/CylinderYRigidBody/GripperUpper/Area")) return;		
		
		GD.Print("is down exited")	;
		
		ToggleAreaBox(GetNode<CSGBox>("Rail/TransportX/IsDownLs"), on: false, sym: _controlIsDownLs);
	}
	
	public void _on_IsUpLs_area_entered(Area area)
	{
		if (area != GetNode("Rail/TransportX/CylinderYRigidBody/Gripper/Area")) return;
		
		GD.Print("is up entered")	;

		
		ToggleAreaBox(GetNode<CSGBox>("Rail/TransportX/IsUpLs"), on: true, sym: _controlIsUpLs);
	}

	public void _on_IsUpLs_area_exited(Area area)
	{
		if (area != GetNode("Rail/TransportX/CylinderYRigidBody/Gripper/Area")) return;	
		
		GD.Print("is up exited")	;
			
		ToggleAreaBox(GetNode<CSGBox>("Rail/TransportX/IsUpLs"), on: false, sym: _controlIsUpLs);
	}

	public void _moveup()
	{
		var sym = _controlCylinderY;
		try
		{
			_ads?.WriteAny((uint)sym.IndexGroup, (uint)sym.IndexOffset, new PLC.Types.ZApplication_ActuatorDigitalComSubscribe { MovePlus = 1 });
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}

	public void _movedown()
	{
		var sym = _controlCylinderY;
		try
		{
			_ads?.WriteAny((uint)sym.IndexGroup, (uint)sym.IndexOffset, new PLC.Types.ZApplication_ActuatorDigitalComSubscribe { MoveMinus = 1 });
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}

	public void _magnetOn()
	{
		var sym = _controlMagnetOn;
		try
		{
			_ads?.WriteAny((uint)sym.IndexGroup, (uint)sym.IndexOffset, new PLC.Types.ZApplication_DigitalComSubscribe { Enable = 1, Write = 1 });
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}
	public void _magnetOff()
	{
		var sym = _controlMagnetOn;
		try
		{
			_ads?.WriteAny((uint)sym.IndexGroup, (uint)sym.IndexOffset, new PLC.Types.ZApplication_DigitalComSubscribe { Enable = 0, Write = 1 });
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}

	public void _conveyorOn()
	{
		var sym = _controlConveyorOn;
		try
		{
			_ads?.WriteAny((uint)sym.IndexGroup, (uint)sym.IndexOffset, new PLC.Types.ZApplication_DigitalComSubscribe { Enable = 1, Write = 1 });
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}
	public void _conveyorOff()
	{
		var sym = _controlConveyorOn;
		try
		{
			_ads?.WriteAny((uint)sym.IndexGroup, (uint)sym.IndexOffset, new PLC.Types.ZApplication_DigitalComSubscribe { Enable = 0, Write = 1 });
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
			var sym = _controlTransportXPosition;
			try
			{
				_ads?.WriteAny((uint)sym.IndexGroup, (uint)sym.IndexOffset, new PLC.Types.ZEquipment_AxisManualPositionComSubscribe { Position1 = -3.5, Speed = 3, MoveAbsolute1 = 1 });
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
			var sym = _controlTransportXPosition;
			try
			{
				_ads?.WriteAny((uint)sym.IndexGroup, (uint)sym.IndexOffset, new PLC.Types.ZEquipment_AxisManualPositionComSubscribe { Position1 = 3.5, Speed = 3, MoveAbsolute1 = 1 });
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
			var sym = _controlTransportXBase;
			try
			{
				_ads?.WriteAny((uint)sym.IndexGroup, (uint)sym.IndexOffset, new PLC.Types.ZEquipment_AxisManualBaseFunctionComSubscribe { Stop = 1 });
			}
			catch (Exception ex)
			{
				DisconnectPlc(ex);
			}
		}
	}

	public void _stop()
	{
		var sym = _controlRequest;
		try
		{
			_ads?.WriteSymbol(sym, new PLC.Types.UselessBoxComRequest { Stop = 1 });
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}

	public void _start()
	{
		var sym = _controlRequest;
		try
		{
			_ads?.WriteSymbol(sym, new PLC.Types.UselessBoxComRequest { Start = 1 });
		}
		catch (Exception ex)
		{
			DisconnectPlc(ex);
		}
	}

	public void _gohome()
	{
		var sym = _controlRequest;
		try
		{
			_ads?.WriteSymbol(sym, new PLC.Types.UselessBoxComRequest { Gohome = 1 });
		}
		catch (Exception ex)
		{
			GD.Print("gohome ex");
			
			DisconnectPlc(ex);
		}
	}
	
	public void _enteredMagnet(Area area)
	{
		if (area != _cube.GetNode("Area"))
			return;

		GD.Print("cube entered magnetic zone");
		_inMagnetZone = true;
	}

	public void _exitedMagnet(Area area)
	{
		if (area != _cube.GetNode("Area"))
			return;

		GD.Print("cube exited magnetic zone");
		_inMagnetZone = false;
	}
	
	public void _ignoreError()
	{
		_showException = false;
		GetNode<ColorRect>("../MessageLayer/ErrorRect").Visible = _showException;
	}
}
