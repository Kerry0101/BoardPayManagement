namespace BoardPaySystem.Models
{
    public class BuildingListViewModel
    {
        public int BuildingId { get; set; }
        public string BuildingName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int TotalFloors { get; set; }
        public int TotalRooms { get; set; }
        public int OccupiedRooms { get; set; }
        public int VacantRooms => TotalRooms - OccupiedRooms;
        public double OccupancyRate => TotalRooms == 0 ? 0 : (OccupiedRooms * 100.0 / TotalRooms);
    }
}