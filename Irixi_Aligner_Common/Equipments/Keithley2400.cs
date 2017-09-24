﻿using GalaSoft.MvvmLight.Command;
using Irixi_Aligner_Common.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace Irixi_Aligner_Common.Equipments
{
    /// <summary>
    /// Class of Keithley 2400
    /// Contains the low-level operation functions in this class, and it's ready to bind to the view
    /// The default unit in this class is A/V/Ohm
    /// </summary>
    public class Keithley2400 : EquipmentBase
    {
        #region Definition
        const double PROT_AMPS_DEF = 0.000105; // default compliance current is set to 105uA
        const double PROT_AMPS_MIN = 1.05; // maximum compliance current is set to 1.05A
        const double PROT_AMPS_MAX = 1.05; // minimum compliance current is set to 1.05A

        const double PROT_VOLT_DEF = 21; // default compliance voltage is set to 21V
        const double PROT_VOLT_MIN = 210; // maximum compliance voltage is set to 21V
        const double PROT_VOLT_MAX = 210; // minmum compliance voltage is set to 21V

        const double MEAS_SPEED_DEF = 1; // default measurement speed is set to 1 for 60Hz power line cycling
        SerialPort serialport;

        public enum EnumInOutTerminal
        {
            FRONT, REAR
        }

        /// <summary>
        /// 分析源
        /// </summary>
        public enum EnumMeasFunc
        {
            OFFALL, ONVOLT, ONCURR, ONRES
        }
        /// <summary>
        /// 分析Range
        /// </summary>
        public enum EnumMeasRange
        {
            REAL, UP, DOWN, MAX, MIN, DEFAULT
        }

        /// <summary>
        /// 源
        /// </summary>
        public enum EnumSourceMode { VOLT, CURR, MEM }

        /// <summary>
        /// 源工作类型
        /// </summary>
        public enum EnumSourceWorkMode { FIX, LIST, SWP }

        /// <summary>
        /// 
        /// </summary>
        public enum EnumSourceRange { REAL, UP, DOWN, MAX, MIN, DEFAULT, AUTO }

        /// <summary>
        /// Options of compliance setting
        /// </summary>
        public enum EnumComplianceLIMIT {DEFAULT, MAX, MIN, REAL }

        /// <summary>
        /// Options of which measurement result to be read
        /// </summary>
        public enum EnumReadCategory { VOLT = 0, CURR }
        
        /// <summary>
        /// Elements contained in the data string for commands :FETCh/:READ/:MEAS/:TRAC:DATA
        /// </summary>
        [Flags]
        public enum EnumDataStringElements
        {
            VOLT = 0x1,
            CURR = 0x2,
            RES = 0x4,
            TIME = 0x8,
            STAT = 0x10,
            ALL = VOLT | CURR | RES | TIME | STAT
        }

        /// <summary>
        /// see page 355 of the manual for the definiations of each bit
        /// </summary>
        [Flags]
        public enum EnumOperationStatus
        {
            OFLO = 0x1,
            FILTER = 0x2,
            FRONTREAR = 0x4,
            CMPL = 0x8,
            OVP = 0x10,
            MATH = 0x20,
            NULL = 0x40,
            LIMITS = 0x80,
            LIMITRET0 = 0x100,
            LIMITRET1 = 0x200,
            AUTOOHMS = 0x400,
            VMEAS = 0x800,
            IMEAS = 0x1000,
            RMEAS = 0x2000,
            VSOUR = 0x4000,
            ISOUR = 0x8000,
            RANGECMPL = 0x10000,
            OFFSETCMPS = 0x20000,
            CONTRACTFAIL = 0x40000,
            TESTRET0 = 0x80000,
            TESTRET1 = 0x100000,
            TESTRET2 = 0x200000,
            RMTSENSE = 0x400000,
            PULSEMODE = 0x800000
        }
        #endregion

        #region Variables
        CancellationTokenSource cts_fetching;
        Task task_fetch_loop = null;

        EnumSourceMode source_mode = EnumSourceMode.VOLT;
        EnumInOutTerminal inout_terminal = EnumInOutTerminal.FRONT;
        EnumMeasFunc measurement_func = EnumMeasFunc.OFFALL;
        EnumDataStringElements data_string_elements = EnumDataStringElements.ALL;
        double meas_speed = MEAS_SPEED_DEF;
        bool is_output_enabled = false;
        double voltage_level = 0, current_level = 0;
        double measured_val = 0;
        double cmpl_voltage = PROT_VOLT_DEF, cmpl_current = PROT_AMPS_DEF;
        private double range_amps = 0, range_volts = 0, range_ohms = 0;
        bool is_auto_range_amps = true, is_auto_range_volts = true, is_auto_range_ohms = true;
        bool is_in_range_cmpl = false, is_meas_over_range = false;
        #endregion

        #region Constructor
        public Keithley2400(ConfigurationKeithley2400 Config):base(Config)
        {
            this.Config = Config;
            serialport = new SerialPort(Config.Port, Config.BaudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 5000
            };
        }
        #endregion

        #region Properties

        /// <summary>
        /// Get or set the output source mode
        /// </summary>
        public EnumSourceMode SourceMode
        {
            private set
            {
                UpdateProperty(ref source_mode, value);
            }
            get
            {
                return source_mode;
            }
        }

        public EnumMeasFunc MeasurementFunc
        {
            private set
            {
                UpdateProperty(ref measurement_func, value);
            }
            get
            {
                return measurement_func;
            }
        }

        /// <summary>
        /// Get the measurement speed by NPLC (Number of Power Line Cycles)
        /// </summary>
        public double MeasurementSpeed
        {
            private set
            { 
                UpdateProperty(ref meas_speed, value);
            }
            get
            {
                return meas_speed;
            }
        }

        /// <summary>
        /// Get whether the output is enabled
        /// </summary>
        public bool IsOutputEnabled
        {
            private set
            {
                UpdateProperty(ref is_output_enabled, value);
            }
            get
            {
                return is_output_enabled;
            }
        }

        /// <summary>
        /// Get which output panel is valid
        /// </summary>
        public EnumInOutTerminal InOutTerminal
        {
            private set
            {
                UpdateProperty(ref inout_terminal, value);
            }
            get
            {
                return inout_terminal;
            }
        }

        /// <summary>
        /// Get the valid measurement value according to current setting, unit A/V
        /// </summary>
        public double MeasurementValue
        {
            private set
            {
                UpdateProperty(ref measured_val, value);
            }
            get
            {
                return measured_val;
            }
        }

        /// <summary>
        /// Get whether the auto range of amps is enabled
        /// </summary>
        public bool IsAutoRangeOfAmps
        {
            private set
            {
                UpdateProperty(ref is_auto_range_amps, value);
            }
            get
            {
                return is_auto_range_amps;
            }
        }

        /// <summary>
        /// Get whether the auto range of volts is enabled
        /// </summary>
        public bool IsAutoRangeOfVolts
        {
            private set
            {
                UpdateProperty(ref is_auto_range_volts, value);
            }
            get
            {
                return is_auto_range_volts;
            }
        }

        /// <summary>
        /// Get whether the auto range of ohms is enabled
        /// </summary>
        public bool IsAutoRangeOfOhms
        {
            private set
            {
                UpdateProperty(ref is_auto_range_ohms, value);
            }
            get
            {
                return is_auto_range_ohms;
            }
        }

        /// <summary>
        /// Get the range of current measurement, unit A
        /// </summary>
        public double MeasRangeOfAmps
        {
            private set
            {
                UpdateProperty(ref range_amps, value);
            }
            get
            {
                return range_amps;
            }
        }

        /// <summary>
        /// Get the range of voltage measurement, unit V
        /// </summary>
        public double MeasRangeOfVolts
        {
            private set
            {
                UpdateProperty(ref range_volts, value);
            }
            get
            {
                return range_volts;
            }
        }

        /// <summary>
        /// Get the range of resister measurement, unit ohm
        /// </summary>
        public double MeasRangeOfOhms
        {
            private set
            {
                UpdateProperty(ref range_ohms, value);
            }
            get
            {
                return range_ohms;
            }
        }

        /// <summary>
        /// Get the voltage set by user in V
        /// </summary>
        public double OutputVoltageLevel
        {
            set
            {
                UpdateProperty(ref voltage_level, value);
            }
            get
            {
                return voltage_level;
            }
        }

        /// <summary>
        /// Get the current set by user in A
        /// </summary>
        public double OutputCurrentLevel
        {
            set
            {
                UpdateProperty(ref current_level, value);
            }
            get
            {
                return current_level;
            }
        }

        /// <summary>
        /// Get compliance in voltage source mode
        /// </summary>
        public double ComplianceVoltage
        {
            private set
            {
                UpdateProperty(ref cmpl_voltage, value);
            }
            get
            {
                return cmpl_voltage;
            }
        }

        /// <summary>
        /// Get compliance in current source mode
        /// </summary>
        public double ComplianceCurrent
        {
            private set
            {
                UpdateProperty(ref cmpl_current, value);
            }
            get
            {
                return cmpl_current;
            }
        }

        public ConfigurationKeithley2400 Config
        {
            private set;
            get;
        }

        /// <summary>
        /// Get specified data elements for data string
        /// </summary>
        public EnumDataStringElements DataStringElements
        {
            private set
            {
                data_string_elements = value;
            }
            get
            {
                return data_string_elements;
            }
        }

        /// <summary>
        /// Get whether measurement was made while in over-range
        /// </summary>
        public bool IsMeasOverRange
        {
            private set
            {
                UpdateProperty(ref is_meas_over_range, value);
            }
            get
            {
                return is_meas_over_range;
            }
        }

        /// <summary>
        /// Get whether in range compliance
        /// </summary>
        public bool IsInRangeCompliance
        {
            private set
            {
                UpdateProperty(ref is_in_range_cmpl, value);
            }
            get
            {
                return is_in_range_cmpl;
            }
        }

        #endregion

        #region General Methods
        /// <summary>
        /// Set the SourceMeter to V-Source Mode
        /// </summary>
        public void SetToVoltageSource()
        {
            SetOutputState(false);
            SetMeasurementFunc(Keithley2400.EnumMeasFunc.ONCURR);
            SetSourceMode(Keithley2400.EnumSourceMode.VOLT);
            SetRangeOfVoltageSource(Keithley2400.EnumSourceRange.AUTO);

            // only return current measurement value under V-Source
            SetDataElement(EnumDataStringElements.CURR | EnumDataStringElements.STAT);
        }

        /// <summary>
        /// Set the SourceMeter to I-Source Mode
        /// </summary>
        public void SetToCurrentSource()
        {
            SetOutputState(false);
            SetMeasurementFunc(Keithley2400.EnumMeasFunc.ONVOLT);
            SetSourceMode(Keithley2400.EnumSourceMode.CURR);
            SetRangeOfCurrentSource(Keithley2400.EnumSourceRange.AUTO);

            // only return voltage measurement value under I-Source
            SetDataElement(EnumDataStringElements.VOLT | EnumDataStringElements.STAT); 
        }
        #endregion

        #region Keithley 2400 Low-level Operating Methods

        #region Common

        /// <summary>
        /// Reset the device to default setting and clear the error query
        /// </summary>
        public void Reset()
        {
            Send(":SYST:CLE");
            Thread.Sleep(50);

            Send("*RST"); // reset device to default setting
            Thread.Sleep(200);
        }

        public string GetDescription()
        {
            return Read("*IDN?");
            
        }

        public void SetOutputState(bool IsEnabled)
        {
            if (IsEnabled)
            {
                Send("OUTP ON");
            }
            else
            {
                // the fetching loop MUST be stopped before turn off the output
                StopAutoFetching();

                Send("OUTP OFF");
                SetBeep(700, 0.2);
                this.MeasurementValue = 0;
            }

            this.IsOutputEnabled = IsEnabled;
        }

        public void GetOutputState()
        {
            var ret = Read("OUTP?");
            if (bool.TryParse(ret, out bool r))
                this.IsOutputEnabled = r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }
        
        /// <summary>
        /// Set which In/Out terminal is valid
        /// </summary>
        /// <param name="Terminal">Front panel / Rear panel</param>
        public void SetInOutTerminal(EnumInOutTerminal Terminal)
        {
            switch(Terminal)
            {
                case EnumInOutTerminal.FRONT:
                    Send(":ROUT:TERM FRON");
                    break;

                case EnumInOutTerminal.REAR:
                    Send(":ROUT:TERM REAR");
                    break;
            }
        }

        public void GetInOutTerminal()
        {
            var ret = Read(":ROUT:TERM?");

            if (ret.Contains("FRON"))
                this.InOutTerminal = EnumInOutTerminal.FRONT;
            else if (ret.Contains("REAR"))
                this.InOutTerminal = EnumInOutTerminal.REAR;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }

        #endregion

        #region Format Subsystem
        /// <summary>
        /// Set the elements valid while executing :Read/etc. commands
        /// </summary>
        /// <param name="Elements"></param>
        public void SetDataElement(EnumDataStringElements Elements)
        {
            List<string> elemlsit = new List<string>();

            if(Elements.HasFlag(EnumDataStringElements.VOLT))
                elemlsit.Add(EnumDataStringElements.VOLT.ToString());

            if(Elements.HasFlag(EnumDataStringElements.CURR))
                elemlsit.Add(EnumDataStringElements.CURR.ToString());

            if (Elements.HasFlag(EnumDataStringElements.RES))
                elemlsit.Add(EnumDataStringElements.RES.ToString());

            if (Elements.HasFlag(EnumDataStringElements.TIME))
                elemlsit.Add(EnumDataStringElements.TIME.ToString());

            if (Elements.HasFlag(EnumDataStringElements.STAT))
                elemlsit.Add(EnumDataStringElements.STAT.ToString());

            if (elemlsit.Count == 0)
                throw new ArgumentException(string.Format("the null elemtents passed, {0}", new StackTrace().GetFrame(0).ToString()));
            else
            {
                string arg = String.Join(",", elemlsit.ToArray());
                Send(string.Format("FORM:ELEM {0}", arg));

                this.DataStringElements = Elements;
            }
        }
        #endregion

        #region Sense1 Subsystem

        public void SetMeasurementSpeed(double Nplc)
        {
            Send(string.Format(":SENS:CURR:NPLC {0}", Nplc));
        }
        
        public void GetMeasurementSpeed()
        {
            var ret = Read(":SENS:CURR:NPLC?");
            if (double.TryParse(ret, out double r))
                this.MeasurementSpeed = r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));

        }
        
        public void SetMeasurementFunc(EnumMeasFunc MeasFunc)
        {
            switch (MeasFunc)
            {
                case EnumMeasFunc.OFFALL:
                    Send(":SENS:FUNC:OFF:ALL");
                    break;

                case EnumMeasFunc.ONCURR:
                    Send(":SENS:FUNC:OFF:ALL;:SENS:FUNC:ON \"CURR\"");
                    break;

                case EnumMeasFunc.ONVOLT:
                    Send(":SENS:FUNC:OFF:ALL;:SENS:FUNC:ON \"VOLT\"");
                    break;

                case EnumMeasFunc.ONRES:
                    Send(":SENS:FUNC:OFF:ALL;:SENS:FUNC:ON \"RES\"");
                    break;
            }

            this.MeasurementFunc = MeasFunc;
        }
        
        public void GetMeasurementFunc()
        {
            //CURR:DC
            var ret = Read(":SENS:FUNC?");

            if (ret == "")
                this.MeasurementFunc = EnumMeasFunc.OFFALL;
            else if (ret.Contains("CURR"))
                this.MeasurementFunc = EnumMeasFunc.ONCURR;
            else if (ret.Contains("VOLT"))
                this.MeasurementFunc = EnumMeasFunc.ONVOLT;
            else if (ret.Contains("CURR"))
                this.MeasurementFunc = EnumMeasFunc.ONRES;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));

        }
        
        public void SetAutoRangeOfAmps(bool IsAutoRange)
        {
            if(IsAutoRange)
            {
                Send(":SENS:CURR:RANG:AUTO 1");
            }
            else
            {
                Send(":SENS:CURR:RANG:AUTO 0");
            }

            this.IsAutoRangeOfAmps = IsAutoRange;
            GetMeasRangeOfAmps();
        }

        public void GetAutoRangeOfAmps()
        {
            var ret = Read(":SENS:CURR:RANG:AUTO?");

            if (int.TryParse(ret, out int r))
            {
                if (r == 1)
                    this.IsAutoRangeOfAmps = true;
                else
                    this.IsAutoRangeOfAmps = false;
            }
            else
            {
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
            }
        }

        public void SetMeasRangeOfAmps(EnumMeasRange Range, double Real = -1)
        {
            switch (Range)
            {
                case EnumMeasRange.MAX:
                    Send(":SENS:CURR:RANG MAX");
                    break;

                case EnumMeasRange.MIN:
                    Send(":SENS:CURR:RANG MIN");
                    break;

                case EnumMeasRange.UP:
                    Send(":SENS:CURR:RANG UP");
                    break;

                case EnumMeasRange.DOWN:
                    Send(":SENS:CURR:RANG DOWN");
                    break;

                case EnumMeasRange.DEFAULT:
                    Send(":SENS:CURR:RANG DEF");
                    break;

                case EnumMeasRange.REAL:
                    Send(":SENS:CURR:RANG " + Real.ToString());
                    break;
            }

            this.IsAutoRangeOfAmps = false;
            GetMeasRangeOfAmps();
        }

        public void GetMeasRangeOfAmps()
        {
            var ret = Read(":SENS:CURR:RANG?");

            if (double.TryParse(ret, out double r))
                this.MeasRangeOfAmps = r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }

        public void SetAutoRangeOfVolts(bool IsAutoRange)
        {
            if (IsAutoRange)
            {
                Send(":SENS:VOLT:RANG:AUTO 1");
            }
            else
            {
                Send(":SENS:VOLT:RANG:AUTO 0");
            }

            this.IsAutoRangeOfVolts = IsAutoRange;
            GetMeasRangeOfVolts();
        }

        public void GetAutoRangeOfVolts()
        {
            var ret = Read(":SENS:VOLT:RANG:AUTO?");
            if (int.TryParse(ret, out int r))
            {
                if (r == 1)
                    this.IsAutoRangeOfVolts = true;
                else
                    this.IsAutoRangeOfVolts = false;
            }
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }

        public void SetMeasRangeOfVolts(EnumMeasRange Range, double Real = -1)
        {
            switch (Range)
            {
                case EnumMeasRange.MAX:
                    Send(":SENS:VOLT:RANG MAX:");
                    break;

                case EnumMeasRange.MIN:
                    Send(":SENS:VOLT:RANG MIN:");
                    break;

                case EnumMeasRange.UP:
                    Send(":SENS:VOLT:RANG UP:");
                    break;

                case EnumMeasRange.DOWN:
                    Send(":SENS:VOLT:RANG DOWN:");
                    break;

                case EnumMeasRange.DEFAULT:
                    Send(":SENS:VOLT:RANG DEF:");
                    break;

                case EnumMeasRange.REAL:
                    Send(":SENS:VOLT:RANG " + Real);
                    break;
            }

            this.IsAutoRangeOfVolts = false;
            GetMeasRangeOfVolts();
        }

        public void GetMeasRangeOfVolts()
        {
            var ret = Read(":SENS:VOLT:RANG?");

            if (double.TryParse(ret, out double r))
                this.MeasRangeOfVolts = r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }

        #endregion
        
        #region Source Subsystem
        
        public void SetSourceMode(EnumSourceMode Mode)
        {
            switch (Mode)
            {
                case EnumSourceMode.CURR:
                    Send(":SOUR:FUNC CURR");
                    break;

                case EnumSourceMode.VOLT:
                    Send(":SOUR:FUNC VOLT");
                    break;

                case EnumSourceMode.MEM:
                    Send(":SOUR:FUNC MEM");
                    break;
            }

            this.SourceMode = Mode;
        }

        public void GetSourceMode()
        {
            var ret = Read(":SOUR:FUNC?");
            
            if (ret.Contains("CURR"))
                this.SourceMode = EnumSourceMode.CURR;
            else if (ret.Contains("VOLT"))
                this.SourceMode = EnumSourceMode.VOLT;
            else if (ret.Contains("MEM"))
                this.SourceMode = EnumSourceMode.MEM;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }
        
        public void SetSourcingModeOfCurrentSource(EnumSourceWorkMode Mode)
        {
            switch (Mode)
            {
                case EnumSourceWorkMode.FIX:
                    Send(":SOUR:CURR:MODE FIX");
                    break;

                case EnumSourceWorkMode.LIST:
                    Send(":SOUR:CURR:MODE LIST");
                    break;

                case EnumSourceWorkMode.SWP:
                    Send(":SOUR:CURR:MODE SWP");
                    break;
            }
        }
        
        public void SetSourcingModeOfVoltageSource(EnumSourceWorkMode Mode)
        {
            switch (Mode)
            {
                case EnumSourceWorkMode.FIX:
                    Send(":SOUR:VOLT:MODE FIX");
                    break;

                case EnumSourceWorkMode.LIST:
                    Send(":SOUR:VOLT:MODE LIST");
                    break;

                case EnumSourceWorkMode.SWP:
                    Send(":SOUR:VOLT:MODE SWP");
                    break;
            }
        }
        
        public void SetRangeOfCurrentSource(EnumSourceRange Range, double Real = -1)
        {
            switch (Range)
            {
                case EnumSourceRange.AUTO:
                    Send(":SOUR:CURR:RANG:AUTO 1");
                    break;

                case EnumSourceRange.DEFAULT:
                    Send(":SOUR:CURR:RANG DEF");
                    break;

                case EnumSourceRange.DOWN:
                    Send(":SOUR:CURR:RANG DOWN");
                    break;

                case EnumSourceRange.UP:
                    Send(":SOUR:CURR:RANG UP");
                    break;

                case EnumSourceRange.MIN:
                    Send(":SOUR:CURR:RANG MIN");
                    break;

                case EnumSourceRange.MAX:
                    Send(":SOUR:CURR:RANG MAX");
                    break;

                case EnumSourceRange.REAL:
                    Send(":SOUR:CURR:RANG " + Real.ToString());
                    break;
            }
        }
        
        public void SetRangeOfVoltageSource(EnumSourceRange Range, double Real = -1)
        {
            switch (Range)
            {
                case EnumSourceRange.AUTO:
                    Send(":SOUR:VOLT:RANG:AUTO 1");
                    break;

                case EnumSourceRange.DEFAULT:
                    Send(":SOUR:VOLT:RANG DEF");
                    break;

                case EnumSourceRange.DOWN:
                    Send(":SOUR:VOLT:RANG DOWN");
                    break;

                case EnumSourceRange.UP:
                    Send(":SOUR:VOLT:RANG UP");
                    break;

                case EnumSourceRange.MIN:
                    Send(":SOUR:VOLT:RANG MIN");
                    break;

                case EnumSourceRange.MAX:
                    Send(":SOUR:VOLT:RANG MAX");
                    break;

                case EnumSourceRange.REAL:
                    Send(":SOUR:VOLT:RANG " + Real.ToString());
                    break;
            }
        }
        
        public void SetComplianceCurrent(EnumComplianceLIMIT Cmpl, double Real = -1)
        {
            switch (Cmpl)
            {
                case EnumComplianceLIMIT.DEFAULT:
                    Send(":SENS:CURR:PROT DEF");
                    this.ComplianceCurrent = PROT_AMPS_DEF;
                    break;

                case EnumComplianceLIMIT.MIN:
                    Send(":SENS:CURR:PROT MIN");
                    this.ComplianceCurrent = PROT_AMPS_MIN;
                    break;

                case EnumComplianceLIMIT.MAX:
                    Send(":SENS:CURR:PROT MAX");
                    this.ComplianceCurrent = PROT_AMPS_MAX;
                    break;

                case EnumComplianceLIMIT.REAL:
                    Send(":SENS:CURR:PROT " + Real.ToString());
                    this.ComplianceCurrent = Real;
                    break;
            }

            GetComplianceCurrent();
        }

        public void GetComplianceCurrent()
        {
            var ret = Read(":SENS:CURR:PROT?");

            if (double.TryParse(ret, out double r))
                this.ComplianceCurrent = r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }
        
        public void SetComplianceVoltage(EnumComplianceLIMIT Cmpl, double Real = -1)
        {
            switch (Cmpl)
            {
                case EnumComplianceLIMIT.DEFAULT:
                    Send(":SENS:VOLT:PROT DEF");
                    this.ComplianceVoltage = PROT_VOLT_DEF;
                    break;

                case EnumComplianceLIMIT.MIN:
                    Send(":SENS:VOLT:PROT MIN");
                    this.ComplianceVoltage = PROT_VOLT_MIN;
                    break;

                case EnumComplianceLIMIT.MAX:
                    Send(":SENS:VOLT:PROT MAX");
                    this.ComplianceVoltage = PROT_VOLT_MAX;
                    break;

                case EnumComplianceLIMIT.REAL:
                    Send(":SENS:VOLT:PROT " + Real.ToString());
                    this.ComplianceVoltage = Real;
                    break;
            }

            GetComplianceVoltage();
        }

        public void GetComplianceVoltage()
        {
            var ret = Read(":SENS:VOLT:PROT?");

            if (double.TryParse(ret, out double r))
                this.ComplianceVoltage = r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }
        
        public void SetVoltageSourceLevel(double Voltage)
        {
            Send(":SOUR:VOLT:LEV " + Voltage.ToString());
            this.OutputVoltageLevel = Voltage;
        }

        public void GetVoltageSourceLevel()
        {
            var ret = Read(":SOUR:VOLT:LEV?");

            if (double.TryParse(ret, out double r))
                this.OutputVoltageLevel = r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }
        
        public void SetCurrentSourceLevel(double Current)
        {
            Send(":SOUR:CURR:LEV " + Current.ToString());
            this.OutputCurrentLevel = Current;
        }

        public void GetCurrentSourceLevel()
        {
            var ret = Read(":SOUR:CURR:LEV?");

            if (double.TryParse(ret, out double r))
                this.OutputCurrentLevel = r;
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));

        }
        #endregion

        #region System Subsystem
        /// <summary>
        /// Remove the SourceMeter from the remote state and enables the operation of front panel keys
        /// </summary>
        public void SetExitRemoteState()
        {
            Send(":SYST:LOC");
        }

        /// <summary>
        /// Control beeper
        /// </summary>
        /// <param name="Frequency"></param>
        /// <param name="Duration"></param>
        public void SetBeep(double Frequency, double Duration)
        {
            if (Frequency < 65 || Frequency > 2000000)
            {
                throw new ArgumentException(string.Format("the argument frequency is invalid, {0}", new StackTrace().GetFrame(0).ToString()));
            }
            else if (Duration < 0 || Duration > 7.9)
            {
                throw new ArgumentException(string.Format("the argument duration is invalid, {0}", new StackTrace().GetFrame(0).ToString()));
            }
            else
            {
                
                Send(string.Format(":SYST:BEEP {0},{1}", Frequency, Duration));
            }
        }

        /// <summary>
        /// Enable or disable beeper
        /// </summary>
        /// <param name="IsEnabled"></param>
        public void SetBeeperState(bool IsEnabled)
        {
            if(IsEnabled)
                Send(":SYST:BEEP:STAT ON");
            else
                Send(":SYST:BEEP:STAT OFF");
        }

        #endregion

        #region Display Subsystem
        /// <summary>
        /// Enable or disable the front display circuitry, when disabled, the instrument operates at a higher speed
        /// </summary>
        /// <param name="IsEnabled"></param>
        public void SetDisplayCircuitry(bool IsEnabled)
        {
            if (IsEnabled)
                Send(":DISP:ENAB ON");
            else
                Send(":DISP:ENAB OFF");
        }

        /// <summary>
        /// Enable or disable the text message display function
        /// </summary>
        /// <param name="WinId"></param>
        /// <param name="IsEnabled"></param>
        public void SetDisplayTextState(int WinId, bool IsEnabled)
        {
            if (WinId != 1 && WinId != 2)
            {
                throw new ArgumentOutOfRangeException(string.Format("window id is error, {0}", new StackTrace().GetFrame(0).ToString()));
            }
            else
            {
                if (IsEnabled)
                    Send(string.Format(":DISP:WIND{0}:TEXT:STAT ON", WinId));
                else
                    Send(string.Format(":DISP:WIND{0}:TEXT:STAT OFF", WinId));
            }
        }

        /// <summary>
        /// Set the message displayed on the screen
        /// </summary>
        /// <param name="WinId"></param>
        /// <param name="Message"></param>
        public void SetDisplayTextMessage(int WinId, string Message)
        {
            if (WinId != 1 && WinId != 2)
            {
                throw new ArgumentOutOfRangeException(string.Format("window id is error, {0}", new StackTrace().GetFrame(0).ToString()));
            }
            else
            {
                if (WinId == 1 && Message.Length > 20)
                {
                    throw new ArgumentOutOfRangeException(string.Format("the length of message on top display can not be greater then 20, {0}", new StackTrace().GetFrame(0).ToString()));
                }
                else if (WinId == 2 && Message.Length > 32)
                {
                    throw new ArgumentOutOfRangeException(string.Format("the length of message on bottom display can not be greater then 32, {0}", new StackTrace().GetFrame(0).ToString()));
                }
                else
                {
                    Send(string.Format(":DISP:WIND{0}:TEXT:DATA \"{1}\"", WinId, Message));
                }
            }
        }
        #endregion 

        #endregion

        #region Override Methods

        public override Task<bool> Init()
        {
            return new Task<bool>(() =>
            {
                try
                {
                    serialport.Open();

                    // wait for the K2400 to ready to receive commands
                    //e.g. Grandfather stayed up to wait for them to come to our house
                    Task.Delay(100);

                    // reset to default setting and clear the error query
                    Reset();
 
                    string desc = this.GetDescription();
                    if (desc.IndexOf("MODEL 2400") > -1)
                    {

                        // switch the source mode to v-source
                        SetToVoltageSource();

                        // disable original display
                        SetDisplayCircuitry(true);

                        // enable user message display
                        SetDisplayTextState(1, true);
                        SetDisplayTextState(2, true);

                        // show user messages
                        SetDisplayTextMessage(1, this.Config.Caption);
                        SetDisplayTextMessage(2, "powered by IRIXI ALIGNER");

                        // enable beeper
                        SetBeeperState(true);
                        
                        this.IsInitialized = true;
                        return true;
                    }
                    else
                    {
                        this.LastError = string.Format("the device connected to the port {0} might not be Keithley 2400", this.Port);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    this.LastError = ex.Message;
                    return false;
                }
            });
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public override double Fetch()
        {
            var ret = Read(":READ?");
            string[] meas_ret = ret.Split(',');
            if (double.TryParse(meas_ret[0], out double r))
            {
                // if the operation status has been requested
                if(this.DataStringElements.HasFlag(EnumDataStringElements.STAT))
                {
                    // parse the status from data string
                    if(double.TryParse(meas_ret[meas_ret.Length - 1], out double stat_tmp))
                    {
                        var status = (EnumOperationStatus)stat_tmp;
                        
                        // check flag of over-range
                        if (status.HasFlag(EnumOperationStatus.OFLO))
                            this.IsMeasOverRange = true;
                        else
                            this.IsMeasOverRange = false;

                        // check flag of range compliance
                        if (status.HasFlag(EnumOperationStatus.RANGECMPL))
                            this.IsInRangeCompliance = true;
                        else
                            this.IsInRangeCompliance = false;
                    }
                }

                return r;
            }
            else
                throw new InvalidCastException(string.Format("unknown value {0} returned, {1}", ret, new StackTrace().GetFrame(0).ToString()));
        }

        public override Task<double> FetchAsync()
        {
            return new Task<double>(() =>
            {
                return Fetch();
            });
        }

        public override void StartAutoFetching()
        {
            // check whether the task had been started
            if (task_fetch_loop == null || task_fetch_loop.IsCompleted)
            {
                // token source to cancel the task
                cts_fetching = new CancellationTokenSource();
                var token = cts_fetching.Token;

                // start the loop task
                task_fetch_loop = Task.Run(() =>
                {
                    // disable display to speed up instrument operation
                    SetDisplayCircuitry(false);

                    while (!token.IsCancellationRequested)
                    {
                        this.MeasurementValue = Fetch();
                        Task.Delay(10);
                    }

                    // resume display
                    SetDisplayCircuitry(true);
                });
            }
        }

        /// <summary>
        /// Stop the fetching loop task
        /// </summary>
        public override void StopAutoFetching()
        {
            if (task_fetch_loop != null && task_fetch_loop.IsCompleted == false)
            {
                // cancel the task of fetching loop
                cts_fetching.Cancel();
                TimeSpan ts = TimeSpan.FromMilliseconds(3000);
                if (!task_fetch_loop.Wait(ts))
                    throw new TimeoutException("unable to stop the fetching loop task");
                else
                {
                    // destory the objects
                    task_fetch_loop = null;
                    cts_fetching = null;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        // turn off output
                        SetOutputState(false);

                        // enable display
                        SetDisplayCircuitry(true);

                        // remove remote state
                        SetExitRemoteState();

                        // close serial port and destory the object
                        serialport.Close();
                        serialport = null;
                    }
                    catch(Exception ex)
                    {
                        ;
                    }
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        #endregion

        #region Private Methods
        /// <summary>
        /// Send command to 2400 and check if any errors occured
        /// </summary>
        /// <param name="Command"></param>
        /// <returns></returns>
        private void Send(string Command)
        {
            try
            {
                lock (serialport)
                {
                    serialport.WriteLine(Command);

                    Thread.Sleep(10);

                    // check if error occured
                    serialport.WriteLine(":SYST:ERR:CODE?");
                    var ret = serialport.ReadLine();

                    if (int.TryParse(ret, out int err_code))
                    {
                        if (err_code != 0) // error occured
                        {
                            // read all errors occured
                            serialport.WriteLine(":SYST:ERR:ALL?");
                            this.LastError = serialport.ReadLine();
                            throw new InvalidOperationException(this.LastError);
                        }
                    }
                    else
                    {
                        this.LastError = string.Format("unable to convert error code {0} to number", err_code);
                        throw new InvalidCastException(this.LastError);
                    }
                }
            }
            catch(Exception ex)
            {
                this.LastError = ex.Message;
                throw ex;
            }
        }

        private string Read(string Command)
        {

            try
            {
                lock (serialport)
                {
                    serialport.WriteLine(Command);
                    return serialport.ReadLine().Replace("\r", "").Replace("\n", "");
                }
            }
            catch(TimeoutException)
            {
                // read all errors occured
                serialport.WriteLine(":SYST:ERR:ALL?");
                this.LastError = serialport.ReadLine();
                throw new InvalidOperationException(this.LastError);
            }
            catch(Exception ex)
            {
                this.LastError = ex.Message;
                throw ex;
            }
        }

        #endregion
    }
}
