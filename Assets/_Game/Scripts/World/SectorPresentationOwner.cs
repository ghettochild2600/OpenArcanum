namespace Arcanum.World
{
    /// <summary>A presentation boundary driven by the authoritative runtime sector selector.</summary>
    public interface ISectorPresentationOwner
    {
        string ConfiguredSector { get; }
        string PresentedSector { get; }
        bool IsSectorPresented { get; }
        bool PresentSector(string sectorPath);
        void ClearPresentedSector();
    }

    /// <summary>The single authority through which runtime sector changes are requested.</summary>
    public interface ISectorSelectionAuthority
    {
        string SelectedSector { get; }
        bool HasSelectedSector { get; }
        bool SelectSector(string sectorPath);
    }

    /// <summary>Lets a presentation-only browser forward selection requests without owning selection.</summary>
    public interface ISectorSelectionClient
    {
        void BindSelectionAuthority(ISectorSelectionAuthority authority);
    }
}
