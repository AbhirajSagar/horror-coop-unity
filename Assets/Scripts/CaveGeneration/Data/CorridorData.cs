namespace CaveGeneration.Data
{
    /// <summary>
    /// Represents a corridor connection connecting two rooms by their indices.
    /// </summary>
    [System.Serializable]
    public struct CorridorData
    {
        public int roomA;
        public int roomB;

        public CorridorData(int roomA, int roomB)
        {
            this.roomA = roomA;
            this.roomB = roomB;
        }
    }
}
