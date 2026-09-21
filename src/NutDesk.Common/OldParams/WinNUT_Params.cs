// WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
// Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace NutDesk.Common.OldParams
{
    /// <summary>
    /// Previous system of persistent settings storage for WinNUT, in the Windows <see cref="Registry"/>.
    /// </summary>
    public class NutDesk_Params
    {
        private const string REG_KEY_ROOT = @"SOFTWARE\WinNUT\";

        private static readonly Dictionary<string, Dictionary<string, object>> DEFAULT_PARAM_STRUCTURE =
            new Dictionary<string, Dictionary<string, object>>
            {
                { "Connexion", new Dictionary<string, object>
                    {
                        { "ServerAddress", "nutserver host" },
                        { "Port", 3493 },
                        { "UPSName", "UPSName" },
                        { "Delay", 1000 },
                        { "NutLogin", new SerializedProtectedString(string.Empty) },
                        { "NutPassword", new SerializedProtectedString(string.Empty) },
                        { "AutoReconnect", false }
                    }
                },
                { "Appareance", new Dictionary<string, object>
                    {
                        { "MinimizeToTray", false },
                        { "MinimizeOnStart", false },
                        { "CloseToTray", false },
                        { "StartWithWindows", false }
                    }
                },
                { "Calibration", new Dictionary<string, object>
                    {
                        { "MinInputVoltage", 210 },
                        { "MaxInputVoltage", 270 },
                        { "FrequencySupply", 0 },
                        { "MinInputFrequency", 40 },
                        { "MaxInputFrequency", 60 },
                        { "MinOutputVoltage", 210 },
                        { "MaxOutputVoltage", 250 },
                        { "MinBattVoltage", 6 },
                        { "MaxBattVoltage", 18 }
                    }
                },
                { "Power", new Dictionary<string, object>
                    {
                        { "ShutdownLimitBatteryCharge", 30 },
                        { "ShutdownLimitUPSRemainTime", 120 },
                        { "ImmediateStopAction", false },
                        { "Follow_FSD", false },
                        { "TypeOfStop", 0 },
                        { "DelayToShutdown", 15 },
                        { "AllowExtendedShutdownDelay", false },
                        { "ExtendedShutdownDelay", 15 }
                    }
                },
                { "Logging", new Dictionary<string, object>
                    {
                        { "UseLogFile", false },
                        { "Log Level", 0 }
                    }
                },
                { "Update", new Dictionary<string, object>
                    {
                        { "VerifyUpdate", false },
                        { "VerifyUpdateAtStart", false },
                        { "DelayBetweenEachVerification", 2 },
                        { "StableOrDevBranch", 0 },
                        { "LastDateVerification", DateTime.MinValue }
                    }
                }
            };

        private Dictionary<string, Dictionary<string, object>> _Parameters;

        public Dictionary<string, Dictionary<string, object>> Parameters
        {
            get => _Parameters;
            protected set => _Parameters = value;
        }

        public static RegistryKey RegistryKeyRoot => Registry.CurrentUser.OpenSubKey(REG_KEY_ROOT, true);

        public static bool ParamsExist => RegistryKeyRoot != null;

        /// <summary>
        /// Load parameters from the Windows User Registry Hive.
        /// </summary>
        /// <returns>At a bare minimum, an empty Dictionary structure. Depending on set parameters, this will return
        /// filled structures with either default preferences or loaded preferences.</returns>
        protected static Dictionary<string, Dictionary<string, object>> LoadParams(object callingObj)
        {
            var newParams = new Dictionary<string, Dictionary<string, object>>();

            if (RegistryKeyRoot != null)
            {
                foreach (KeyValuePair<string, Dictionary<string, object>> ParamStructFolder in DEFAULT_PARAM_STRUCTURE)
                {
                    var regKeyFolder = RegistryKeyRoot.OpenSubKey(ParamStructFolder.Key);

                    if (regKeyFolder != null)
                    {
                        var paramFolder = new Dictionary<string, object>();

                        foreach (KeyValuePair<string, object> ParamItem in ParamStructFolder.Value)
                        {
                            try
                            {
                                var WinReg = ChangeTypeDynamic(regKeyFolder.GetValue(ParamItem.Key), ParamItem.Value.GetType());
                                paramFolder.Add(ParamItem.Key, WinReg);
                                AppGlobals.LogFile.LogTracing("Loaded parameter " + ParamItem.Key, LogLvl.LOG_NOTICE, callingObj);
                            }
                            catch (Exception ex)
                            {
                                AppGlobals.LogFile.LogTracing(string.Format("Failed to load value from Registry [{0}\\{1}]:{2}{3}",
                                        regKeyFolder.Name, ParamItem.Key, Environment.NewLine, ex.Message), LogLvl.LOG_WARNING, callingObj);
                            }
                        }

                        newParams.Add(ParamStructFolder.Key, paramFolder);
                    }
                    else
                    {
                        AppGlobals.LogFile.LogTracing("Failed to open or create Registry Key for " + ParamStructFolder.Key, LogLvl.LOG_WARNING, callingObj);
                    }
                }
            }
            else
            {
                AppGlobals.LogFile.LogTracing("Failed to open the root WinNUT key.", LogLvl.LOG_ERROR, callingObj);
            }

            return newParams;
        }

        /// <summary>
        /// Stand-in for VB's CTypeDynamic, which performed a late-bound conversion of a value to a type known
        /// only at run time. Like CTypeDynamic it honours user-defined conversion operators - the login and
        /// password come out of the Registry as Strings and have to become
        /// <see cref="SerializedProtectedString"/> - and otherwise defers to <see cref="Convert.ChangeType(object, Type)"/>.
        /// A missing Registry value (Nothing) yields the target type's default, again matching CTypeDynamic.
        /// </summary>
        private static object ChangeTypeDynamic(object value, Type targetType)
        {
            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            var conversion = FindConversionOperator(value.GetType(), targetType);

            if (conversion != null)
            {
                return conversion.Invoke(null, new[] { value });
            }

            return Convert.ChangeType(value, targetType, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Look for a widening or narrowing conversion operator from <paramref name="sourceType"/> to
        /// <paramref name="targetType"/>, declared on either of the two types.
        /// </summary>
        private static MethodInfo FindConversionOperator(Type sourceType, Type targetType)
        {
            foreach (var declaringType in new[] { targetType, sourceType })
            {
                foreach (var method in declaringType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name != "op_Implicit" && method.Name != "op_Explicit")
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();

                    if (targetType.IsAssignableFrom(method.ReturnType) && parameters.Length == 1
                            && parameters[0].ParameterType.IsAssignableFrom(sourceType))
                    {
                        return method;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Exports the WinNUT Registry Key structure using the reg executable to a .reg file.
        /// </summary>
        /// <param name="destinationPath">The location of the exported file.</param>
        public static void ExportParams(string destinationPath)
        {
            // Duplicate file must be removed now otherwise reg.exe will hang waiting for an input to overwrite.
            if (File.Exists(destinationPath))
            {
                // Allow any exceptions to be passed up to caller.
                File.Delete(destinationPath);
            }

            var regExpProcStartInfo = new ProcessStartInfo
            {
                FileName = "reg.exe",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Arguments = "export \"" + RegistryKeyRoot.Name + "\" \"" + destinationPath + "\""
            };

            var proc = Process.Start(regExpProcStartInfo);
            proc.WaitForExit();
            if (proc.ExitCode == 1)
            {
                throw new InvalidOperationException("reg.exe encountered an error: " + proc.StandardError.ReadToEnd());
            }
        }

        public static void DeleteParams()
        {
            RegistryKeyRoot.DeleteSubKeyTree("");
        }
    }
}
