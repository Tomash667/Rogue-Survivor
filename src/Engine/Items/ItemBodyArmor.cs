﻿using RogueSurvivor.Data;
using RogueSurvivor.Gameplay;
using System;

namespace RogueSurvivor.Engine.Items
{
    [Serializable]
    class ItemBodyArmor : Item
    {
        public int Protection_Hit { get; private set; }
        public int Protection_Shot { get; private set; }
        public int Encumbrance { get; private set; }
        public int Weight { get; private set; }

        public ItemBodyArmor(ItemModel model)
            : base(model)
        {
            if (!(model is ItemBodyArmorModel))
                throw new ArgumentException("model is not a BodyArmorModel");

            ItemBodyArmorModel m = model as ItemBodyArmorModel;
            this.Protection_Hit = m.Protection_Hit;
            this.Protection_Shot = m.Protection_Shot;
            this.Encumbrance = m.Encumbrance;
            this.Weight = m.Weight;
        }

        public bool IsHostileForCops()
        {
            return Array.IndexOf(GameFactions.BAD_POLICE_OUTFITS, (GameItems.IDs)Model.ID) >= 0;
        }

        public bool IsFriendlyForCops()
        {
            return Array.IndexOf(GameFactions.GOOD_POLICE_OUTFITS, (GameItems.IDs)Model.ID) >= 0;
        }

        public bool IsHostileForBiker(GameGangs.IDs gangID)
        {
            return Array.IndexOf(GameGangs.BAD_GANG_OUTFITS[(int)gangID], (GameItems.IDs)Model.ID) >= 0;
        }

        public bool IsFriendlyForBiker(GameGangs.IDs gangID)
        {
            return Array.IndexOf(GameGangs.GOOD_GANG_OUTFITS[(int)gangID], (GameItems.IDs)Model.ID) >= 0;
        }
    }
}