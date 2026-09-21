// WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
// Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using System.Collections.Generic;

namespace NutDesk.Common.OldParams
{
    /// <summary>
    /// Helper class to upgrade parameters.
    /// </summary>
    public class UpgradableParams : NutDesk_Params
    {
        private List<PrefSettingPair> _PrefSettingsLookup = new List<PrefSettingPair>
        {
            new PrefSettingPair("ServerAddress", "NUT_ServerAddress"),
            new PrefSettingPair("Port", "NUT_ServerPort"),
            new PrefSettingPair("UPSName", "NUT_UPSName"),
            new PrefSettingPair("Delay", "NUT_PollIntervalMsec"),
            new PrefSettingPair("NutLogin", "NUT_Username"),
            new PrefSettingPair("NutPassword", "NUT_Password"),
            new PrefSettingPair("AutoReconnect", "NUT_AutoReconnect"),
            new PrefSettingPair("MinInputVoltage", "CAL_VoltInMin"),
            new PrefSettingPair("MaxInputVoltage", "CAL_VoltInMax"),
            new PrefSettingPair("FrequencySupply", "CAL_FreqInNom"),
            new PrefSettingPair("MinInputFrequency", "CAL_FreqInMin"),
            new PrefSettingPair("MaxInputFrequency", "CAL_FreqInMax"),
            new PrefSettingPair("MinOutputVoltage", "CAL_VoltOutMin"),
            new PrefSettingPair("MaxOutputVoltage", "CAL_VoltOutMax"),
            new PrefSettingPair("MinBattVoltage", "CAL_BattVMin"),
            new PrefSettingPair("MaxBattVoltage", "CAL_BattVMax"),
            new PrefSettingPair("MinimizeToTray", "MinimizeToTray"),
            new PrefSettingPair("MinimizeOnStart", "MinimizeOnStart"),
            new PrefSettingPair("CloseToTray", "CloseToTray"),
            new PrefSettingPair("StartWithWindows", "StartWithWindows"),
            new PrefSettingPair("UseLogFile", "LG_LogToFile"),
            new PrefSettingPair("Log Level", "LG_LogLevel"),
            new PrefSettingPair("ShutdownLimitBatteryCharge", "PW_BattChrgFloor"),
            new PrefSettingPair("ShutdownLimitUPSRemainTime", "PW_RuntimeFloor"),
            new PrefSettingPair("ImmediateStopAction", "PW_Immediate"),
            new PrefSettingPair("Follow_FSD", "PW_RespectFSD"),
            new PrefSettingPair("TypeOfStop", "PW_StopType"),
            new PrefSettingPair("DelayToShutdown", "PW_StopDelaySec"),
            new PrefSettingPair("AllowExtendedShutdownDelay", "PW_UserExtendStopTimer"),
            new PrefSettingPair("ExtendedShutdownDelay", "PW_ExtendDelaySec"),
            new PrefSettingPair("VerifyUpdate", "UP_AutoUpdate"),
            new PrefSettingPair("VerifyUpdateAtStart", "UP_CheckAtStart"),
            new PrefSettingPair("DelayBetweenEachVerification", "UP_AutoChkDelay"),
            new PrefSettingPair("StableOrDevBranch", "UP_Branch"),
            new PrefSettingPair("LastDateVerification", "UP_LastCheck")
        };

        public List<PrefSettingPair> PrefSettingsLookup
        {
            get => _PrefSettingsLookup;
            set => _PrefSettingsLookup = value;
        }

        private int lastCount = -1;
        public int TotalPrefs
        {
            get
            {
                if (lastCount == -1) // Calculate, then cache the result.
                {
                    lastCount = 0;

                    foreach (KeyValuePair<string, Dictionary<string, object>> collection in Parameters)
                    {
                        lastCount += collection.Value.Count;
                    }
                }

                return lastCount;
            }
        }

        public UpgradableParams()
        {
            Parameters = LoadParams(this);
        }

        public class PrefSettingPair
        {
            public string OldPreferenceName { get; set; }
            public string NewSettingsName { get; set; }

            public PrefSettingPair(string oldPrefName, string newSettingName)
            {
                OldPreferenceName = oldPrefName;
                NewSettingsName = newSettingName;
            }
        }

        internal class UpgradeWorkerArguments
        {
            public bool DoImport { get; }
            public bool DoBackup { get; }
            public bool DoDelete { get; }

            public UpgradeWorkerArguments(bool doImport, bool doBackup, bool doDelete)
            {
                DoImport = doImport;
                DoBackup = doBackup;
                DoDelete = doDelete;
            }
        }

        internal class UpgradeWorkerProgressReport
        {
            public string LogOutput { get; set; }
            public LogLvl LogLevel { get; set; }
            public object Sender { get; set; }
            public string LogResourceString { get; set; }

            public UpgradeWorkerProgressReport(string logOutput, LogLvl logLevel, object sender, string logRes = null)
            {
                LogOutput = logOutput;
                LogLevel = logLevel;
                Sender = sender;
                LogResourceString = logRes;
            }
        }
    }
}
