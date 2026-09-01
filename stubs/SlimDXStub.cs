using System;
using System.Collections.Generic;
using System.Reflection;

[assembly: AssemblyVersion("4.0.10.43")]

namespace SlimDX
{
	public struct Result
	{
		public int Code { get; set; }
		public bool IsSuccess { get { return Code >= 0; } }
		public bool IsFailure { get { return Code < 0; } }
		public static Result Last { get { return new Result(); } }
	}

	namespace DirectInput
	{
		public class DirectInputException : Exception
		{
			public DirectInputException() { }
			public Result Result { get { return new Result(); } }
		}

		public enum DeviceClass
		{
			All,
			Device,
			Pointer,
			Keyboard,
			Mouse,
			Joystick,
			GameController,
			Media,
			ForceFeedback,
			Other
		}

		[Flags]
		public enum DeviceEnumerationFlags
		{
			AllDevices = 0,
			AttachedOnly = 1
		}

		[Flags]
		public enum ObjectDeviceType
		{
			All = 0,
			Device = 1,
			Keyboard = 2,
			Mouse = 3,
			Pointer = 4,
			ForceFeedbackController = 5,
			Axis = 6,
			Button = 7,
			PovController = 8
		}

		public class DeviceInstance
		{
			public Guid InstanceGuid { get; set; }
			public Guid ProductGuid { get; set; }
		}

		public struct DeviceObjectInstance
		{
			public ObjectDeviceType ObjectType { get; set; }
		}

		public class DeviceProperties
		{
			public string PortDisplayName { get { return ""; } }
		}

		public class ObjectProperties
		{
			public void SetRange(int lowerRange, int upperRange) { }
		}

		public class JoystickState
		{
			public int X { get; set; }
			public int Y { get; set; }
			public int Z { get; set; }
			public int RotationZ { get; set; }
			public bool[] GetButtons() { return new bool[0]; }
			public int[] GetPointOfViewControllers() { return new int[0]; }
		}

		public class Device : IDisposable
		{
			public DeviceProperties Properties { get { return new DeviceProperties(); } }
			public Result Acquire() { return new Result(); }
			public Result Unacquire() { return new Result(); }
			public IList<DeviceObjectInstance> GetObjects() { return new List<DeviceObjectInstance>(); }
			public ObjectProperties GetObjectPropertiesById(int objId) { return new ObjectProperties(); }
			public void Dispose() { }
		}

		public class Joystick : Device
		{
			public Joystick(DirectInput directInput, Guid subsystem) { }
			public JoystickState GetCurrentState() { return new JoystickState(); }
		}

		public class DirectInput : IDisposable
		{
			public DirectInput() { }
			public IList<DeviceInstance> GetDevices(DeviceClass deviceClass, DeviceEnumerationFlags deviceEnumerationFlags)
			{
				return new List<DeviceInstance>();
			}
			public void Dispose() { }
		}
	}
}



