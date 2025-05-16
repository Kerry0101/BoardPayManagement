namespace BoardPaySystem.Models
{
    public static class EntityExtensions
    {
        // Safe navigation methods for Room properties
        public static int GetRoomId(this ApplicationUser user)
        {
            return user?.CurrentRoom?.RoomId ?? 0;
        }

        public static decimal GetRoomMonthlyRent(this ApplicationUser user, Building defaultBuilding)
        {
            var customRent = user?.CurrentRoom?.CustomMonthlyRent;
            if (customRent.HasValue)
            {
                return customRent.Value;
            }

            // Try to get from Floor->Building
            var buildingRent = user?.CurrentRoom?.Floor?.Building?.DefaultMonthlyRent;
            if (buildingRent.HasValue)
            {
                return buildingRent.Value;
            }

            // Fall back to provided building default
            return defaultBuilding?.DefaultMonthlyRent ?? 0m;
        }

        public static decimal GetRoomWaterFee(this ApplicationUser user, Building defaultBuilding)
        {
            var customFee = user?.CurrentRoom?.CustomWaterFee;
            if (customFee.HasValue)
            {
                return customFee.Value;
            }

            // Try to get from Floor->Building
            var buildingFee = user?.CurrentRoom?.Floor?.Building?.DefaultWaterFee;
            if (buildingFee.HasValue)
            {
                return buildingFee.Value;
            }

            // Fall back to provided building default
            return defaultBuilding?.DefaultWaterFee ?? 0m;
        }

        public static decimal GetRoomElectricityFee(this ApplicationUser user, Building defaultBuilding)
        {
            var customFee = user?.CurrentRoom?.CustomElectricityFee;
            if (customFee.HasValue)
            {
                return customFee.Value;
            }

            // Try to get from Floor->Building
            var buildingFee = user?.CurrentRoom?.Floor?.Building?.DefaultElectricityFee;
            if (buildingFee.HasValue)
            {
                return buildingFee.Value;
            }

            // Fall back to provided building default
            return defaultBuilding?.DefaultElectricityFee ?? 0m;
        }

        public static decimal GetRoomWifiFee(this ApplicationUser user, Building defaultBuilding)
        {
            var customFee = user?.CurrentRoom?.CustomWifiFee;
            if (customFee.HasValue)
            {
                return customFee.Value;
            }

            // Try to get from Floor->Building
            var buildingFee = user?.CurrentRoom?.Floor?.Building?.DefaultWifiFee;
            if (buildingFee.HasValue)
            {
                return buildingFee.Value;
            }

            // Fall back to provided building default
            return defaultBuilding?.DefaultWifiFee ?? 0m;
        }
    }
}