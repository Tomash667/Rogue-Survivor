﻿using RogueSurvivor.Data;
using System;

namespace RogueSurvivor.Engine.Items
{
    [Serializable]
    class ItemTracker : Item
    {
        int m_Batteries;

        public ItemTrackerModel.TrackingFlags Tracking { get; private set; }

        public bool CanTrackFollowersOrLeader
        {
            get { return (this.Tracking & ItemTrackerModel.TrackingFlags.FOLLOWER_AND_LEADER) != 0; }
        }

        public bool CanTrackUndeads
        {
            get { return (this.Tracking & ItemTrackerModel.TrackingFlags.UNDEADS) != 0; }
        }

        public bool CanTrackBlackOps
        {
            get { return (this.Tracking & ItemTrackerModel.TrackingFlags.BLACKOPS_FACTION) != 0; }
        }

        public bool CanTrackPolice
        {
            get { return (this.Tracking & ItemTrackerModel.TrackingFlags.POLICE_FACTION) != 0; }
        }

        public bool HasClock
        {
            get { return (this.Model as ItemTrackerModel).HasClock; }
        }

        public int Batteries
        {
            get { return m_Batteries; }
            set
            {
                if (value < 0) value = 0;
                m_Batteries = Math.Min(value, (this.Model as ItemTrackerModel).MaxBatteries);
            }
        }

        public bool IsFullyCharged
        {
            get { return m_Batteries >= (this.Model as ItemTrackerModel).MaxBatteries; }
        }

        public ItemTracker(ItemModel model)
            : base(model)
        {
            if (!(model is ItemTrackerModel))
                throw new ArgumentException("model is not a TrackerModel");

            ItemTrackerModel m = model as ItemTrackerModel;
            this.Tracking = m.Tracking;
            this.Batteries = m.MaxBatteries;
        }
    }
}
