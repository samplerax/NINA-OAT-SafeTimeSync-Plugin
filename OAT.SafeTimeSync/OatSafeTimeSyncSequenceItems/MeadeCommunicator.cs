using NINA.Equipment.Interfaces.Mediator;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System;
using System.Threading.Tasks;

namespace Samplerax.NINA.OatSafeTimeSync.OatSafeTimeSyncTestCategory
{
    internal sealed class MeadeCommunicator : IDisposable
    {
        private readonly ITelescopeMediator? mediator;
        private readonly Type? mediatorType;
        private readonly Action<string> logger;

        public MeadeCommunicator(ITelescopeMediator? mediator, Action<string>? logger = null)
        {
            this.mediator = mediator;
            this.mediatorType = mediator?.GetType();
            this.logger = logger ?? (_ => { });
        }

        private void Log(string s) => logger.Invoke(s);

        public bool IsAvailable => mediator is not null;

        public bool IsConnected()
        {
            if (mediator is null || mediatorType is null) return false;
            try
            {
                var mGetDevice = mediatorType.GetMethod("GetDevice", BindingFlags.Instance | BindingFlags.Public);
                if (mGetDevice is not null)
                {
                    var dev = mGetDevice.Invoke(mediator, null);
                    if (dev is not null)
                    {
                        var p = dev.GetType().GetProperty("Connected") ?? dev.GetType().GetProperty("IsConnected");
                        if (p?.GetValue(dev) is bool b) return b;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"IsConnected check error: {ex.Message}");
            }
            return false;
        }

        private static string FormatDebug(string? s)
        {
            if (s is null) return "(null)";
            if (s.Length == 0) return "(empty)";
            var sb = new StringBuilder();
            var trimmed = s.Replace("\r", "\\r").Replace("\n", "\\n");
            sb.Append(trimmed)
              .Append(" [len=").Append(s.Length).Append(']');
            try
            {
                var bytes = Encoding.ASCII.GetBytes(s);
                sb.Append(" hex=");
                for (int i = 0; i < bytes.Length && i < 64; i++)
                    sb.Append(bytes[i].ToString("X2"));
            }
            catch
            {
            }
            return sb.ToString();
        }

        private static double? TryParseNumber(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            try
            {
                if (Regex.Match(raw.Trim(), "^(-?\\d*\\.?\\d+)#?$") is Match m1 && m1.Success
                    && double.TryParse(m1.Groups[1].Value, out double v1))
                    return v1;

                if (Regex.Match(raw, "(-?\\d*\\.?\\d+)") is Match m2 && m2.Success
                    && double.TryParse(m2.Groups[1].Value, out double v2))
                    return v2;
            }
            catch
            {
            }
            return null;
        }

        public async Task<string?> SendCommandAsync(string command)
        {
            if (mediator is null || mediatorType is null)
            {
                Log("MeadeComm: Mediator not available");
                return null;
            }

            if (string.IsNullOrEmpty(command))
            {
                Log("MeadeComm: Empty command");
                return null;
            }

            Log($"MeadeComm: Attempting to send -> {command}");

            try
            {
                var mGetDevice = mediatorType.GetMethod("GetDevice", BindingFlags.Instance | BindingFlags.Public);
                if (mGetDevice is not null)
                {
                    var device = mGetDevice.Invoke(mediator, null);
                    if (device is not null)
                    {
                        Log($"MeadeComm: Got device of type {device.GetType().FullName}");
                        var devType = device.GetType();
                        var cmdStringMethod = devType.GetMethod("CommandString", new Type[] { typeof(string), typeof(bool) });
                        if (cmdStringMethod is not null)
                        {
                            try
                            {
                                Log("MeadeComm: Using CommandString method via reflection");
                                string cleanCmd = command.TrimEnd('#') + "#";
                                Log($"MeadeComm: Sending command -> {cleanCmd}");

                                string? response = await Task.Run(() =>
                                {
                                    try
                                    {
                                        var res = cmdStringMethod.Invoke(device, new object[] { cleanCmd, true });
                                        return res as string;
                                    }
                                    catch (Exception ex)
                                    {
                                        Log($"MeadeComm: Command error -> {ex.Message}");
                                        return null;
                                    }
                                });

                                if (!string.IsNullOrEmpty(response))
                                {
                                    var cleanResponse = response.TrimEnd('#', '\r', '\n', ' ');
                                    Log($"MeadeComm: Raw response -> '{FormatDebug(cleanResponse)}'");

                                    if (TryParseNumber(cleanResponse) is double num)
                                    {
                                        var formatted = num.ToString("F2");
                                        Log($"MeadeComm: Parsed numeric value -> {formatted}");
                                        return formatted;
                                    }

                                    return cleanResponse;
                                }
                                else
                                {
                                    Log("MeadeComm: No response from device");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"MeadeComm: Command error -> {ex.Message}");
                                if (ex.InnerException is not null)
                                {
                                    Log($"MeadeComm: Inner error -> {ex.InnerException.Message}");
                                }
                            }
                        }
                        else
                        {
                            var sendCommandString = devType.GetMethod("SendCommandString") ?? devType.GetMethod("SendString") ?? devType.GetMethod("Command");
                            if (sendCommandString is not null)
                            {
                                try
                                {
                                    Log("MeadeComm: Using SendCommandString/SendString/Command via reflection");
                                    string cleanCmd = command.TrimEnd('#') + "#,#";
                                    Log($"MeadeComm: Sending command -> {cleanCmd}");

                                    var response = await Task.Run(() =>
                                    {
                                        try
                                        {
                                            return sendCommandString.Invoke(device, new object[] { cleanCmd, true });
                                        }
                                        catch (Exception ex)
                                        {
                                            Log($"MeadeComm: Command error -> {ex.InnerException?.Message ?? ex.Message}");
                                            return null;
                                        }
                                    });

                                    if (response is string strResponse)
                                    {
                                        var cleanResponse = strResponse.TrimEnd('#', '\r', '\n', ' ');
                                        Log($"MeadeComm: Raw response -> '{FormatDebug(cleanResponse)}'");

                                        if (TryParseNumber(cleanResponse) is double num)
                                        {
                                            var formatted = num.ToString("F2");
                                            Log($"MeadeComm: Parsed numeric value -> {formatted}");
                                            return formatted;
                                        }

                                        if (!string.IsNullOrEmpty(cleanResponse))
                                        {
                                            return cleanResponse;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log($"MeadeComm: Command error -> {ex.Message}");
                                    if (ex.InnerException is not null)
                                    {
                                        Log($"MeadeComm: Inner error -> {ex.InnerException.Message}");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Log("MeadeComm: GetDevice returned null");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"MeadeComm: Error: {ex.Message}");
                if (ex.InnerException is not null)
                {
                    Log($"MeadeComm: Inner error: {ex.InnerException.Message}");
                }
            }

            return null;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
