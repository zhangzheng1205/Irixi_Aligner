﻿using GalaSoft.MvvmLight;
using Irixi_Aligner_Common.Configuration;
using Irixi_Aligner_Common.Configuration.Equipments;
using Irixi_Aligner_Common.Equipments.Base;
using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Threading;

namespace Irixi_Aligner_Common.Equipments.Instruments
{
    public class Newport2832C : InstrumentBase
    {
        #region Definition

        const int MAX_CH = 2;

        public enum EnumChannel
        {
            A,
            B
        }

        /// <summary>
        /// Valid current measurement range
        /// </summary>
        public enum EnumRange
        {
            [Description("AUTO")]
            AUTO,

            [Description("RANGE 0")]
            R0 = 0,

            [Description("RANGE 1")]
            R1,

            [Description("RANGE 2")]
            R2,

            [Description("RANGE 3")]
            R3,

            [Description("RANGE 4")]
            R4,

            [Description("RANGE 5")]
            R5,

            [Description("RANGE 6")]
            R6
        }

        public enum EnumUnits
        {
            [Description("A")]
            A,  // amps

            [Description("W")]
            W,  // watts

            [Description("W/cm")]
            W_cm, //watts/cm^3

            [Description("dBm")]
            dBm,

            [Description("dB")]
            dB,

            [Description("REL")]
            REL
        }

        [Flags]
        public enum EnumStatusFlag
        {
            OVERRANGE,
            SATURATED,
            DATAERR,
            RANGING
        }

        #endregion

        #region Variables
        
        #endregion

        #region Constructor

        public Newport2832C(ConfigurationNewport2832C Config) : base(Config)
        {
            this.Config = Config;
            IsMultiChannel = true;

            // create meta properties for each channel
            MetaProperties = new Newport2832C_MetaProperies[MAX_CH];
            for (int i = 0; i < MAX_CH; i++)
            {
                MetaProperties[i] = new Newport2832C_MetaProperies();
            }

            serialport = new SerialPort(Config.Port, Config.BaudRate)
            {
                ReadTimeout = 500
            };

        }

        #endregion

        #region Properties

        public new ConfigurationNewport2832C Config { get; }
        
        public Newport2832C_MetaProperies[] MetaProperties
        {
            get;
        }

        #endregion


        #region Override Methods

        protected override void UserInitProc()
        {
            string desc = this.GetDescription();
            if (desc.ToUpper().IndexOf("2832-C") > -1)
            {
                // reset to default setting and clear the error query
                Reset();

                // Reset settings of channel A
                SetDisplayChannel(EnumChannel.A);
                SetMeasurementRange(EnumChannel.A, EnumRange.AUTO);
                SetLambda(EnumChannel.A, 1550);
                SetUnit(EnumChannel.A, EnumUnits.W);

                // Reset settings of channel B
                SetDisplayChannel(EnumChannel.B);
                SetMeasurementRange(EnumChannel.B, EnumRange.AUTO);
                SetLambda(EnumChannel.B, 1550);
                SetUnit(EnumChannel.B, EnumUnits.W);

                // Reset settings of channel A
                this.ActiveChannel = (int)EnumChannel.A;

                this.IsInitialized = true;

                // Start to auto fetch process
                StartAutoFetching();

            }
            else
            {
                throw new Exception("the identification is error");
            }

        }
        
        public override double Fetch()
        {
            var retgrp = Read("RWS?");

            var ret = retgrp.Split(',');
            if (ret.Length != 4)
                throw new FormatException("the length of the response of the RWS request is error");

            // parse status flag
            for (int i = 0; i < MAX_CH; i++)
            {
                if (int.TryParse(ret[i], out int stat))
                    MetaProperties[i].Status = (EnumStatusFlag)stat;
                else
                    throw new FormatException("the STATUS part of the response of the RWS request is error");
            }

            // parse power value
            for (int i = 0; i < MAX_CH; i++)
            {
                if (double.TryParse(ret[i + 2], out double pwr))
                    MetaProperties[i].MeasurementValue = pwr;
                else
                    throw new FormatException("the POWER part of the response of the RWS request is error");
            }
            
            return MetaProperties[ActiveChannel].MeasurementValue;
        }

        public override double Fetch(int Channel)
        {
            Fetch();
            return MetaProperties[ActiveChannel].MeasurementValue;
        }

        protected override void DoAutoFetching(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Fetch();
                Thread.Sleep(20);
            }
        }

        #endregion

        #region Appropriative Methods of Newport 2832C

        public void SetDisplayChannel(EnumChannel CH)
        {
            ActiveChannel = (int)CH;
            Send(string.Format("DISPCH {0}", CH));
        }
        
        public void SetMeasurementRange(EnumChannel CH, EnumRange Range)
        {
            if (Range == EnumRange.AUTO)
            {
                Send(string.Format("AUTO_{0} 1", CH));

            }
            else
            {
                Send(string.Format("RANGE_{0} {1}", CH, (int)Range));
            }

            MetaProperties[(int)CH].Range = Range;
        }

        public void GetMeasurementRange(EnumChannel CH)
        {
            var ret = Read(string.Format("AUTO_{0}?", CH));
            if (ret == "1")
            {
                MetaProperties[(int)CH].Range = EnumRange.AUTO;
            }
            else if (ret == "0")
            {
                ret = Read(string.Format("RANGE_{0}?", CH));
                if (int.TryParse(ret, out int range))
                {
                    MetaProperties[(int)CH].Range = (EnumRange)range;
                }
                else
                {
                    throw new FormatException(string.Format("the format of the response of RANGE request is error"));
                }
            }
            else
            {
                throw new FormatException(string.Format("the format of the response of AUTO request is error"));
            }
        }

        public void SetLambda(EnumChannel CH, int Lambda)
        {
            Send(string.Format("LAMBDA_{0} {1}", CH, Lambda));
            MetaProperties[(int)CH].Lambda = Lambda;
        }

        public void GetLambda(EnumChannel CH)
        {
            var ret = Read(string.Format("LAMBDA_{0}?", CH));
            if (int.TryParse(ret, out int lambda))
            {
                MetaProperties[(int)CH].Lambda = lambda;
            }
            else
            {
                throw new FormatException(string.Format("the format of the response of LAMBDA request is error"));
            }
        }

        public void SetUnit(EnumChannel CH, EnumUnits Unit)
        {
            if (Unit == EnumUnits.W_cm)
            {
                Send(string.Format("UNITS_{0} \"W/cm\"", CH));
            }
            else
            {
                Send(string.Format("UNITS_{0} \"{1}\"", CH, Unit));
            }

            MetaProperties[(int)CH].Unit = Unit;
        }

        public void GetUnit(EnumChannel CH)
        {
            var ret = Read(string.Format("UNITS_{0}?", CH));
            ret = ret.Replace("\\", "").Replace("\"", "");
            if (ret == "W/cm")
            {
                MetaProperties[(int)CH].Unit = EnumUnits.W_cm;
            }
            else
            {
                MetaProperties[(int)CH].Unit = (EnumUnits)Enum.Parse(typeof(EnumUnits), ret);
            }
        }

        #endregion

    }

    public class Newport2832C_MetaProperies : ViewModelBase
    {
        Newport2832C.EnumRange range;
        Newport2832C.EnumUnits unit;
        Newport2832C.EnumStatusFlag status;

        double measured_val;
        int lambda;
        bool isOverRange, isSaturated, isDataError, isRanging;

        public Newport2832C.EnumStatusFlag Status
        {
            get
            {
                return status;
            }
            set
            {
                status = value;

                if (status.HasFlag(Newport2832C.EnumStatusFlag.DATAERR))
                    IsDataError = true;
                else
                    IsDataError = false;

                if (status.HasFlag(Newport2832C.EnumStatusFlag.OVERRANGE))
                    IsOverRange = true;
                else
                    IsOverRange = false;

                if (status.HasFlag(Newport2832C.EnumStatusFlag.SATURATED))
                    IsSaturated = true;
                else
                    IsSaturated = false;

                if (status.HasFlag(Newport2832C.EnumStatusFlag.RANGING))
                    IsRanging = true;
                else
                    IsRanging = false;

                RaisePropertyChanged();
            }
        }

        public bool IsOverRange
        {
            get
            {
                return isOverRange;
            }
            set
            {
                isOverRange = value;
                RaisePropertyChanged();
            }
        }

        public bool IsSaturated
        {
            get
            {
                return isSaturated;
            }
            set
            {
                isSaturated = value;
                RaisePropertyChanged();
            }
        }

        public bool IsDataError
        {
            get
            {
                return isDataError;
            }
            set
            {
                isDataError = value;
                RaisePropertyChanged();
            }
        }

        public bool IsRanging
        {
            get
            {
                return isRanging;
            }
            set
            {
                isRanging = value;
                RaisePropertyChanged();
            }
        }


        public Newport2832C.EnumRange Range
        {
            set
            {
                range = value;
                RaisePropertyChanged();
            }
            get
            {
                return range;
            }
        }

        public int Lambda
        {
            get
            {
                return lambda;
            }
            set
            {
                lambda = value;
                RaisePropertyChanged();
            }
        }

        public Newport2832C.EnumUnits Unit
        {
            get
            {
                return unit;
            }
            set
            {
                unit = value;
                RaisePropertyChanged();
            }
        }

        public double MeasurementValue
        {
            set
            {
                measured_val = value;
                RaisePropertyChanged();
            }
            get
            {
                return measured_val;
            }
        }
    }
}



