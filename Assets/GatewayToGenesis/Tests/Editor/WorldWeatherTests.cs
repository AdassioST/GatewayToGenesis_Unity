using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class WorldWeatherTests
{
    [Test]
    public void CrisisOverridesLocalFrontThenRevealsItOnExpiry()
    {
        var local = ScriptableObject.CreateInstance<WeatherProfileSO>();
        var crisis = ScriptableObject.CreateInstance<WeatherProfileSO>();
        local.name = "WorldWeatherTest_Local";
        crisis.name = "WorldWeatherTest_Crisis";
        try
        {
            var tile = new WorldTile { coord = HexCoord.Zero };
            var fronts = new List<WorldWeatherFront> {
                new WorldWeatherFront { id = 1, profile = local, radius = 3, remainingSevenths = 0 },
                new WorldWeatherFront { id = 2, profile = crisis, extent = WeatherExtent.World, priority = 100, remainingSevenths = 2 }
            };
            Assert.AreSame(crisis, WorldWeather.At(fronts, tile).profile);
            WorldWeather.Tick(fronts);
            Assert.AreSame(crisis, WorldWeather.At(fronts, tile).profile);
            WorldWeather.Tick(fronts);
            Assert.AreSame(local, WorldWeather.At(fronts, tile).profile);
            Assert.AreEqual(0, fronts[0].remainingSevenths, "Permanent fronts do not expire.");
            fronts.Add(new WorldWeatherFront { id = 3, profile = crisis, radius = 3 });
            Assert.AreSame(crisis, WorldWeather.At(fronts, tile).profile, "Newest wins equal priority.");
            Assert.IsNull(WorldWeather.At(fronts, new WorldTile { coord = new HexCoord(20, 0) }));
            var saved = SaveStateCodec.Write(fronts, typeof(List<WorldWeatherFront>));
            var restored = (List<WorldWeatherFront>)SaveStateCodec.Read(saved, typeof(List<WorldWeatherFront>));
            Assert.AreEqual(2, restored.Count);
            Assert.AreEqual(3, WorldWeather.At(restored, tile).id);
        }
        finally { Object.DestroyImmediate(local); Object.DestroyImmediate(crisis); }
    }
}
