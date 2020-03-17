﻿using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Overlay;
using Microsoft.Extensions.Logging;
using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Sensor
{
    public class SensorService : ISensorService
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<SensorService> _logger;

        private Computer _computer;
        private Dictionary<string, IOverlayEntry> _overlayEntryDict = new Dictionary<string, IOverlayEntry>();

        public bool UseSensorLogging => _appConfiguration.UseSensorLogging;

        public SensorService(IAppConfiguration appConfiguration,
                             ILogger<SensorService> logger)
        {
            _appConfiguration = appConfiguration;
            _logger = logger;

            _logger.LogDebug("{componentName} Ready", this.GetType().Name);

            StartOpenHardwareMonitor();
            InitializeOverlayEntryDict();
        }

        private void StartOpenHardwareMonitor()
        {
            try
            {
                _computer = new Computer();
                //_computer.HardwareAdded += new HardwareEventHandler(h => { });
                //_computer.HardwareRemoved += new HardwareEventHandler(h => { });

                _computer.Open();

                _computer.MainboardEnabled = false;
                _computer.FanControllerEnabled = false;
                _computer.GPUEnabled = true;
                _computer.CPUEnabled = true;
                _computer.RAMEnabled = true;
                _computer.HDDEnabled = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when starting OpenHardwareMonitor");
            }
        }

        private void InitializeOverlayEntryDict()
        {
            if (_computer == null) return;

            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                foreach (var subHardware in hardware.SubHardware)
                {
                    subHardware.Update();
                    foreach (var sensor in subHardware.Sensors)
                    {
                        var currentEntry = CreateOverlayEntry(sensor, hardware.HardwareType);
                        _overlayEntryDict.Add(currentEntry.Identifier, currentEntry);
                    }
                }

                foreach (var sensor in hardware.Sensors)
                {
                    var currentEntry = CreateOverlayEntry(sensor, hardware.HardwareType);
                    _overlayEntryDict.Add(currentEntry.Identifier, currentEntry);
                }
            }
        }

        private IOverlayEntry CreateOverlayEntry(ISensor sensor, HardwareType hardwareType)
        {
            return new OverlayEntryWrapper(sensor.Identifier.ToString())
            {
                Description = sensor.Name,
                OverlayEntryType = MapType(hardwareType),
                GroupName = sensor.Name,
                ShowGraph = false,
                ShowGraphIsEnabled = true,
                ShowOnOverlayIsEnabled = true,
                ShowOnOverlay = true,
                Value = 0,
                ValueFormat = GetFormatString(sensor.SensorType)
            };
        }

        private EOverlayEntryType MapType(HardwareType hardwareType)
        {
            EOverlayEntryType type = EOverlayEntryType.Undefined;
            switch (hardwareType)
            {
                case HardwareType.Mainboard:
                    type = EOverlayEntryType.Mainboard;
                    break;
                case HardwareType.SuperIO:
                    type = EOverlayEntryType.Undefined;
                    break;
                case HardwareType.CPU:
                    type = EOverlayEntryType.CPU;
                    break;
                case HardwareType.RAM:
                    type = EOverlayEntryType.RAM;
                    break;
                case HardwareType.GpuNvidia:
                    type = EOverlayEntryType.GPU;
                    break;
                case HardwareType.GpuAti:
                    type = EOverlayEntryType.GPU;
                    break;
                case HardwareType.TBalancer:
                    type = EOverlayEntryType.Undefined;
                    break;
                case HardwareType.Heatmaster:
                    type = EOverlayEntryType.Undefined;
                    break;
                case HardwareType.HDD:
                    type = EOverlayEntryType.HDD;
                    break;
            }

            return type;
        }

        private string GetFormatString(SensorType sensorType)
        {
            string formatString = "{0}";
            switch (sensorType)
            {
                case SensorType.Voltage:
                    formatString = "{0} V";
                    break;
                case SensorType.Clock:
                    formatString = "{0} MHz";
                    break;
                case SensorType.Temperature:
                    formatString = "{0} °C";
                    break;
                case SensorType.Load:
                    formatString = "{0} %";
                    break;
                case SensorType.Fan:
                    formatString = "{0} RPM";
                    break;
                case SensorType.Flow:
                    formatString = "{0} L/h";
                    break;
                case SensorType.Control:
                    formatString = "{0} %";
                    break;
                case SensorType.Level:
                    formatString = "{0} %";
                    break;
                case SensorType.Factor:
                    formatString = "{0}";
                    break;
                case SensorType.Power:
                    formatString = "{0} W";
                    break;
                case SensorType.Data:
                    formatString = "{0} GB";
                    break;
                case SensorType.SmallData:
                    formatString = "{0} MB";
                    break;
                case SensorType.Throughput:
                    formatString = "{0} MB/s";
                    break;
            }

            return formatString;
        }

        public bool CheckHardwareChanged(List<IOverlayEntry> overlayEntries)
        {
            var overlayEntryIdentfiers = overlayEntries
                    .Select(entry => entry.Identifier)
                    .ToList();
            var overlayEntryLiveIdentfiers = GetSensorOverlayEntries()
                    .Select(entry => entry.Identifier)
                    .ToList(); ;

            return !(overlayEntryIdentfiers.All(overlayEntryLiveIdentfiers.Contains)
                 && overlayEntryIdentfiers.Count == overlayEntryLiveIdentfiers.Count);
        }

        public IOverlayEntry[] GetSensorOverlayEntries()
        {
            return _overlayEntryDict.Values.ToArray();
        }

        public IOverlayEntry GetSensorOverlayEntry(string identifier)
        {
            return _overlayEntryDict[identifier];
        }

        public ISessionSensorData GetSessionSensorData()
        {
            throw new NotImplementedException();
        }

        public void StartSensorLogging()
        {
            throw new NotImplementedException();
        }

        public void StopSensorLogging()
        {
            throw new NotImplementedException();
        }

        public void UpdateSensors()
        {
            if (_computer == null) return;

            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                foreach (var subHardware in hardware.SubHardware)
                {
                    subHardware.Update();
                    foreach (var sensor in subHardware.Sensors)
                    {
                        var currentIdentifier = sensor.Identifier.ToString();
                        _overlayEntryDict[currentIdentifier].Value = sensor.Value;
                    }
                }

                foreach (var sensor in hardware.Sensors)
                {
                    var currentIdentifier = sensor.Identifier.ToString();
                    _overlayEntryDict[currentIdentifier].Value = sensor.Value;
                }
            }
        }

        public void CloseOpenHardwareMonitor()
        {
            _computer?.Close();
        }
    }
}