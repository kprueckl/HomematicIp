﻿using HomematicIp.Data.HomematicIpObjects.Devices;

namespace HomematicIp.Data.HomematicIpObjects.Channels
{
    public abstract class ThermostatChannelBase : DeviceSabotageChannelBase
    {
        public double? TemperatureOffset { get; set; }
    }
}