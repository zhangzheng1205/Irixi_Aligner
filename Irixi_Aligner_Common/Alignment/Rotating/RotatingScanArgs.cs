﻿using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Irixi_Aligner_Common.Alignment.BaseClasses;
using Irixi_Aligner_Common.Classes;
using Irixi_Aligner_Common.Classes.BaseClass;
using Irixi_Aligner_Common.Equipments.Base;
using Irixi_Aligner_Common.Interfaces;
using Irixi_Aligner_Common.MotionControllers.Base;

namespace Irixi_Aligner_Common.Alignment.Rotating
{
    public class RotatingScanArgs : AlignmentArgsBase
    {
        #region Variables

        const string PROP_GRP_ROTATING = "Rotating Settings";
        const string PROP_GRP_LINEAR = "Linear Settings";


        LogicalAxis axisRotating = null, axisLinear = null;
        //double targetPositionDifferentialOfMaxPower = 5, targetPosDiffChangeRate = 10, gapRotating = 1;
        double _linearInterval = 1, _linearRange = 100, _pitch = 750 * 3;

        //2 channel should be detected at the same time, so we need 2 keithley2400s
        IInstrument instrument, instrument2;

        #endregion

        #region Constructors
        
        public RotatingScanArgs(SystemService Service) :base(Service)
        {
            ScanCurve = new ScanCurve();
            ScanCurve2 = new ScanCurve();

            // add the curves to the group
            ScanCurveGroup.Add(ScanCurve);
            ScanCurveGroup.Add(ScanCurve2);
            ScanCurveGroup.Add(ScanCurve.FittingCurve);
            ScanCurveGroup.Add(ScanCurve2.FittingCurve);
            ScanCurveGroup.Add(ScanCurve.MaxPowerConstantLine);
            ScanCurveGroup.Add(ScanCurve2.MaxPowerConstantLine);

            Properties.Add(new Property("AxisRotating"));
            Properties.Add(new Property("Instrument"));
            Properties.Add(new Property("AxisLinear"));
            Properties.Add(new Property("Instrument2"));
            Properties.Add(new Property("LinearInterval"));
            Properties.Add(new Property("LinearRestriction"));
            Properties.Add(new Property("LengthOfChannelStartToEnd"));
            Properties.Add(new Property("MoveSpeed"));

            AxisXTitle = "ΔPosition";
            AxisYTitle = "Intensity";

            PresetProfileManager = new AlignmentArgsPresetProfileManager<RotatingScanArgs, RotatingScanArgsProfile>(this);

        }

        #endregion

        #region Properties

        public override string SubPath
        {
            get
            {
                return "RotatingScan";
            }
        }

        [Browsable(false)]
        public AlignmentArgsPresetProfileManager<RotatingScanArgs, RotatingScanArgsProfile> PresetProfileManager
        {
            get;
        }

        public override IInstrument Instrument
        {
            get => instrument;
            set
            {
                instrument = value;
                RaisePropertyChanged();

                ScanCurve.DisplayName = ((InstrumentBase)instrument).Config.Caption;
            }
        }

        /// <summary>
        /// The instrument to monitor the secondary feedback channel
        /// </summary>
        [Display(Name = "Instrument 2", 
            GroupName = PROP_GRP_COMMON, 
            Description = "The feedback instrument of the last channel")]
        public IInstrument Instrument2
        {
            get => instrument2;
            set
            {
                instrument2 = value;
                RaisePropertyChanged();

                ScanCurve2.DisplayName = ((InstrumentBase)instrument2).Config.Caption;
            }
        }

        [Display(
            Name = "Axis", 
            GroupName = PROP_GRP_ROTATING, 
            Description = "")]
        public LogicalAxis AxisRotating
        {
            get => axisRotating;
            set
            {
                axisRotating = value;
                RaisePropertyChanged();
            }
        }

        //[Display(Name = "Interval", GroupName = PROP_GRP_ROTATING)]
        //[Obsolete("The property is meaningless in the latest alignment logic."), Browsable(false)]
        //public double RotatingInterval
        //{
        //    get => gapRotating;
        //    set
        //    {
        //        gapRotating = value;
        //        RaisePropertyChanged();
        //    }
        //}


        //[Display(Name = "Rotating Restriction", GroupName = PROP_GRP_ROTATING)]
        //[Obsolete("The property is meaningless in  the latest alignment logic."), Browsable(false)]
        //public double RotatingRestriction
        //{
        //    get => rangeRotating;
        //    set
        //    {
        //        rangeRotating = value;
        //        RaisePropertyChanged();
        //    }
        //}

        [Display(Name = "Axis", GroupName = PROP_GRP_LINEAR, Description = "")]
        public LogicalAxis AxisLinear
        {
            get => axisLinear;
            set
            {
                axisLinear = value;
                RaisePropertyChanged();
            }
        }

        [Display(Name = "Interval", GroupName = PROP_GRP_LINEAR, Description = "")]
        public double LinearInterval
        {
            get => _linearInterval;
            set
            {
                _linearInterval = value;
                RaisePropertyChanged();
            }
        }


        [Display(Name = "Range", GroupName = PROP_GRP_LINEAR, Description = "")]
        public double LinearRestriction
        {
            get => _linearRange;
            set
            {
                _linearRange = value;
                RaisePropertyChanged();
            }
        }

        [Display(Name = "Pitch", GroupName = PROP_GRP_COMMON, Description = "The pitch of the two channels used to scan.")]
        public double Pitch
        {
            get => _pitch;
            set
            {
                _pitch = value;
                RaisePropertyChanged();
            }
        }


        /// <summary>
        /// The target differential of the two positions at the max power of each channel
        /// </summary>
        //[Display(Name = "Target ΔPosition", GroupName = PROP_GRP_TARGET)]
        //[Obsolete("The property is meaningless in  the latest alignment logic."), Browsable(false)]
        //public double TargetPositionDifferentialOfMaxPower
        //{
        //    get => targetPositionDifferentialOfMaxPower;
        //    set
        //    {
        //        targetPositionDifferentialOfMaxPower = value;
        //        RaisePropertyChanged();
        //    }
        //}

        /// <summary>
        /// If the Pos. Diff. change rate is less than this value, exit alignment loop, 
        /// the value is in %.
        /// </summary>
        //[Display(Name = "ΔPos Changing Rate", GroupName = PROP_GRP_TARGET)]
        //[Obsolete("The property is meaningless in  the latest alignment logic."), Browsable(false)]
        //public double TargetPosDiffChangeRate
        //{
        //    get => targetPosDiffChangeRate;
        //    set
        //    {
        //        targetPosDiffChangeRate = value;
        //        RaisePropertyChanged();
        //    }
        //}

        /// <summary>
        /// Scan curve of instrument
        /// </summary>
        [Browsable(false)]
        public ScanCurve ScanCurve { private set; get; }
        
        /// <summary>
        /// Scan curve of instrument2
        /// </summary>
        [Browsable(false)]
        public ScanCurve ScanCurve2 { private set; get; }

        #endregion

        #region Methods
        public override void Validate()
        {
            if(AxisLinear == null)
                throw new ArgumentException("you must specify the linear axis.");

            if (AxisRotating == null)
                throw new ArgumentException("you must specify the rotating axis.");

            if (AxisLinear == AxisRotating)
                throw new ArgumentException("linear axis and rotating axis must be different.");

            if (MoveSpeed < 1 || MoveSpeed > 100)
                throw new ArgumentException("move speed must be between 1 ~ 100");

            if (Instrument == null)
                throw new ArgumentException(string.Format("you must specify the {0}",
                    ((DisplayAttribute)TypeDescriptor.GetProperties(this)["Instrument"].Attributes[typeof(DisplayAttribute)]).Name) ?? "instrument");

            if (Instrument2 == null)
                throw new ArgumentException(string.Format("you must specify the {0}",
                    ((DisplayAttribute)TypeDescriptor.GetProperties(this)["Instrument2"].Attributes[typeof(DisplayAttribute)]).Name) ?? "instrument2");

            if (Instrument == Instrument2)
                throw new ArgumentException("the two instruments must be different.");
        }

        public override void PauseInstruments()
        {
            Instrument.PauseAutoFetching();
            Instrument2.PauseAutoFetching();
        }

        public override void ResumeInstruments()
        {
            Instrument.ResumeAutoFetching();
            Instrument2.ResumeAutoFetching();
        }

        #endregion
    }
}
