using System;
using Arcanum.Formats.World;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Source <c>area_of_object</c> resolution for local maps and the primary START_MAP.</summary>
    public sealed class MapAreaResolver
    {
        private readonly MapList _maps;
        private readonly AreaList _areas;
        private readonly Func<string, bool> _exists;
        private readonly Func<string, byte[]> _read;

        public MapAreaResolver(MapList maps, AreaList areas, Func<string, bool> exists, Func<string, byte[]> read)
        {
            _maps = maps ?? throw new ArgumentNullException(nameof(maps));
            _areas = areas ?? throw new ArgumentNullException(nameof(areas));
            _exists = exists ?? throw new ArgumentNullException(nameof(exists));
            _read = read ?? throw new ArgumentNullException(nameof(read));
        }

        public int Resolve(string selectedSector, Vector2 mapPosition)
        {
            if (!SectorCoordinate.TryParse(selectedSector, out SectorCoordinate selected)) return 0;
            string mapName = selected.MapPath.Substring(selected.MapPath.LastIndexOf('/') + 1);
            if (!_maps.TryGetByName(mapName, out MapListEntry map)) return 0;
            if (map.Area != 0 || map.Type != MapType.StartMap) return map.Area;
            int currentTownMap = TownMap(selected.Path);
            if (currentTownMap == 0) return 0;

            Area nearest = default;
            long nearestDistance = long.MaxValue;
            foreach (Area area in _areas.Areas)
            {
                long distance = Math.Max(Math.Abs(area.TileX - (long)mapPosition.x),
                    Math.Abs(area.TileY - (long)mapPosition.y));
                if (distance >= nearestDistance) continue;
                nearest = area;
                nearestDistance = distance;
            }
            if (nearest.Id == 0) return 0;
            var areaSector = new SectorCoordinate(selected.MapPath,
                (int)(nearest.TileX / SectorCoordinate.Size), (int)(nearest.TileY / SectorCoordinate.Size));
            return TownMap(areaSector.Path) == currentTownMap ? nearest.Id : 0;
        }

        private int TownMap(string sectorPath)
            => _exists(sectorPath) ? SectorReader.ReadSections(_read(sectorPath)).TownMapInfo : 0;
    }
}
