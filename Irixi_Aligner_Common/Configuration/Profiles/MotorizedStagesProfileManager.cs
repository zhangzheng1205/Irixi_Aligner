﻿using System;
using System.Linq;
using static Irixi_Aligner_Common.Classes.BaseClass.RealworldPositionManager;

namespace Irixi_Aligner_Common.Configuration
{
    public class MotorizedStagesProfileManager
    {
        public MotorizedStageProfileContainer[] ProfileContainer { set; get; }

        /// <summary>
        /// Find the profile container by vendor
        /// </summary>
        /// <param name="Vendor"></param>
        /// <returns></returns>
        public MotorizedStageProfileContainer FindProfileContainer(string Vendor)
        {
            var containers = this.ProfileContainer.Where(p => p.Vendor == Vendor);
            if (containers.Any())
                return containers.First();
            else
                return null;
        }


        /// <summary>
        /// Find the profile by vendor and model
        /// </summary>
        /// <param name="Vendor"></param>
        /// <param name="Model"></param>
        /// <returns></returns>
        public MotorizedStageProfile FindProfile(string Vendor, string Model)
        {
            var container = this.FindProfileContainer(Vendor);
            if(container != null)
            {
                var profile = container.Profiles.Where(p => p.Model == Model);
                if (profile.Any())
                    return profile.First();
                else
                    return null;
            }
            else
            {
                return null;
            }
        }        
    }

    public class MotorizedStageProfileContainer
    {
        public string Vendor { get; set; }
        public MotorizedStageProfile[] Profiles {get;set;}
    }

    public class MotorizedStageProfile : ICloneable
    {
        public string Model { set; get; }
        public UnitType Unit { set; get; }
        public double Resolution { set; get; }
        public double TravelDistance { set; get; }

        /// <summary>
        /// recalculate the parameters if the unit is changed
        /// </summary>
        /// <param name="TargetUnit"></param>
        public void ChangeUnit(UnitType TargetUnit)
        {
            // Convert to nm & sec
            switch (this.Unit)
            {
                case UnitType.mm:
                    TravelDistance *= 1000000.0;
                    Resolution *= 1000000.0;
                    break;

                case UnitType.um:
                    TravelDistance *= 1000.0;
                    Resolution *= 1000.0;
                    break;

                case UnitType.deg:
                    TravelDistance *= 3600.0;
                    Resolution *= 3600.0;
                    break;

                case UnitType.min:
                    TravelDistance *= 60.0;
                    Resolution *= 60.0;
                    break;
            }

            // Convert to the specified unit
            switch (TargetUnit)
            {
                case UnitType.mm:
                    TravelDistance /= 1000000.0;
                    Resolution /= 1000000.0;
                    break;

                case UnitType.um:
                    TravelDistance /= 1000.0;
                    Resolution /= 1000.0;
                    break;

                case UnitType.deg:
                    TravelDistance /= 3600.0;
                    Resolution /= 3600.0;
                    break;

                case UnitType.min:
                    TravelDistance /= 60.0;
                    Resolution /= 60.0;
                    break;
            }
            this.Unit = TargetUnit;
        }

        public object Clone()
        {
            return new MotorizedStageProfile()
            {
                Model = this.Model,
                Unit = this.Unit,
                Resolution = this.Resolution,
                TravelDistance = this.TravelDistance
            };
        }
    }
}
