using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Where a Seventh's Unity comes from (per Seventh), before and after happiness.</summary>
public struct UnityBreakdown
{
    public float song, civics, districts, landmarks;
    /// <summary>Happiness's multiplier on all of it.</summary>
    public float multiplier;

    public float Base => song + civics + districts + landmarks;
    public float Total => Base * multiplier;
}

/// <summary>What the people's happiness is read from this Seventh.</summary>
public struct WellbeingInputs
{
    public int population, vagrants;
    /// <summary>Food is scarce (the people go hungry).</summary>
    public bool hungry;
    public float storedValue;
    /// <summary>Kinds of stored food held in quantity.</summary>
    public int variety;
    /// <summary>Luxuries met, 0-1 (<see cref="LuxuryPlan.score"/>).</summary>
    public float luxury;
    /// <summary>Share of the stores' food value that is cooked dishes, 0-1.</summary>
    public float dishShare;
    public float joy;
    /// <summary>The landmarks' living, summed (capped at 1).</summary>
    public float landmarks;
    /// <summary>Living shared tables add (T05: coverage, bounded by HospitalityTuning.coverageLiving; apart from luxuries).</summary>
    public float sharedTables;
}

/// <summary>The people's happiness, 0-100, and its halves.</summary>
public struct Wellbeing
{
    public float survival, living, weight, happiness;
}

/// <summary>One category of luxury this Seventh: what was wanted, what will be taken from which resource, how much was met.</summary>
public class LuxuryDraw
{
    public string category;
    public float want;
    public List<(string resource, float amount)> takes = new List<(string, float)>();
    public List<(string source, float amount)> enjoys = new List<(string, float)>();
    public float faith;
    public float Taken => takes.Sum(t => t.amount);
    public float Met => want <= 0f ? 0f : Math.Min(1f, (Taken + enjoys.Sum(t => t.amount)) / want);
}

public class CulturalAmenity
{
    public string source, category;
    public float amount, faithPerUnit;
}

/// <summary>What the people consume of luxuries this Seventh and how well it meets what they want.</summary>
public class LuxuryPlan
{
    /// <summary>Categories the people want now (more as they grow).</summary>
    public int wanted;
    public List<LuxuryDraw> draws = new List<LuxuryDraw>();
    /// <summary>0-1: the best-met categories, as many as are wanted, averaged (0 when none is wanted).</summary>
    public float score;
    public float Faith => draws.Sum(d => d.faith);
}

/// <summary>
/// The life of the culture, with no scene state (tested in <c>CultureLifeTests</c>):
/// - Unity: song (the Weaver share), civics of music and rite, districts of song, faith and pleasure, landmarks; happy
///   people make more of it.
/// - Happiness: surviving (fed, housed, secure stores, a varied cellar) against living (luxuries, fine dishes, the
///   afterglow of festivals and holidays, landmarks). Living weighs nothing on the frontier and as much as surviving
///   once the people are many.
/// - Luxuries: each category wanted per hundred citizens; more categories wanted as the people grow.
/// - The kitchen: batches a recipe can make from what is held, and what a batch gains in food value.
/// - Holidays: what the next one costs and why it cannot be set apart yet.
/// Every number comes from <see cref="CultureLifeTuning"/>; all are proposals.
/// </summary>
public static class CultureLifeRules
{
    // ===== UNITY =====

    /// <summary>Unity made is multiplied by this: <see cref="CultureLifeTuning.unhappyUnity"/> at happiness 0, 1 at 50, 2 - that at 100.</summary>
    public static float HappinessMultiplier(float happiness, float unhappy)
    {
        float h = Clamp01(happiness / 100f);
        float low = Clamp01(unhappy);
        return low + (1f - low) * 2f * h;
    }

    /// <summary>A Seventh's Unity: song from the Weaver share (and the Song myth), each civic of music or rite, each district of song, faith or pleasure by its development, and landmarks.</summary>
    public static UnityBreakdown Unity(CultureLifeTuning t, float weaverShare, bool songMyth, int unityCivics, IEnumerable<float> riteDistrictDevelopment, float landmarkUnity, float happiness)
    {
        t = t ?? new CultureLifeTuning();
        return new UnityBreakdown
        {
            song = Math.Max(0f, t.songUnity) * Clamp01(weaverShare) + (songMyth ? Math.Max(0f, t.songMythUnity) : 0f),
            civics = Math.Max(0f, t.civicUnity) * Math.Max(0, unityCivics),
            districts = Math.Max(0f, t.districtUnityPer50) * (riteDistrictDevelopment ?? Enumerable.Empty<float>()).Sum(d => Math.Max(0f, d) / 50f),
            landmarks = Math.Max(0f, landmarkUnity),
            multiplier = HappinessMultiplier(happiness, t.unhappyUnity),
        };
    }

    // ===== HAPPINESS =====

    /// <summary>How much living weighs beside surviving: 0 below <see cref="CultureLifeTuning.livingFrom"/> citizens, 1 (as much as surviving) from <see cref="CultureLifeTuning.livingFull"/>.</summary>
    public static float LivingWeight(int population, CultureLifeTuning t)
    {
        t = t ?? new CultureLifeTuning();
        if (t.livingFull <= t.livingFrom) return population >= t.livingFrom ? 1f : 0f;
        return Clamp01((population - t.livingFrom) / (float)(t.livingFull - t.livingFrom));
    }

    /// <summary>Surviving, 0-1: fed (half of it), housed, stores that feel secure, and a varied cellar.</summary>
    public static float Survival(WellbeingInputs x, CultureLifeTuning t)
    {
        t = t ?? new CultureLifeTuning();
        int people = Math.Max(0, x.population) + Math.Max(0, x.vagrants);
        float housed = people <= 0 ? 1f : Math.Max(0, x.population) / (float)people;
        float secure = x.population <= 0 ? 1f : Clamp01(x.storedValue / Math.Max(0.01f, x.population * Math.Max(0.001f, t.securePerCitizen)));
        float varied = Clamp01(x.variety / 3f);
        return Clamp01((x.hungry ? 0f : 0.5f) + 0.2f * housed + 0.2f * secure + 0.1f * varied);
    }

    /// <summary>Living, 0-1: luxuries, fine dishes on the table, the joy of festivals and holidays, landmarks, and a place at shared tables.</summary>
    public static float Living(WellbeingInputs x) =>
        Clamp01(0.4f * Clamp01(x.luxury) + 0.15f * Clamp01(x.dishShare * 2f) + 0.3f * Clamp01(x.joy) + 0.15f * Clamp01(x.landmarks) + Clamp01(x.sharedTables));

    /// <summary>The people's happiness: surviving, and living weighed in as the people grow (at its full weight half and half).</summary>
    public static Wellbeing Happiness(WellbeingInputs x, CultureLifeTuning t)
    {
        float survival = Survival(x, t), living = Living(x), w = LivingWeight(x.population, t);
        return new Wellbeing { survival = survival, living = living, weight = w, happiness = 100f * (survival * (1f - w / 2f) + living * w / 2f) };
    }

    /// <summary>Morale happiness gives or takes: <see cref="CultureLifeTuning.moralePer10Happiness"/> per 10 points from 50, capped.</summary>
    public static int Morale(float happiness, CultureLifeTuning t)
    {
        t = t ?? new CultureLifeTuning();
        int m = (int)Math.Round((happiness - 50f) / 10f * t.moralePer10Happiness, MidpointRounding.AwayFromZero);
        int cap = Math.Max(0, t.happinessMoraleCap);
        return Math.Max(-cap, Math.Min(cap, m));
    }

    public static string MoodWord(float happiness) =>
        happiness < 25f ? "miserable" : happiness < 45f ? "unhappy" : happiness < 60f ? "getting by" : happiness < 75f ? "content" : happiness < 90f ? "happy" : "joyful";

    /// <summary>Joy after a Seventh (it fades) plus what was added.</summary>
    public static float Joy(float joy, float added, float fade) => Clamp01(Clamp01(joy) * (1f - Clamp01(fade)) + Math.Max(0f, added));

    // ===== LUXURIES =====

    /// <summary>Luxury categories the people want: none below <see cref="CultureLifeTuning.livingFrom"/> citizens, then one more every <see cref="CultureLifeTuning.peoplePerLuxury"/>.</summary>
    public static int WantedLuxuries(int population, CultureLifeTuning t)
    {
        t = t ?? new CultureLifeTuning();
        int count = t.luxuries?.Count(l => l != null) ?? 0;
        if (population < t.livingFrom || count == 0) return 0;
        return Math.Min(count, 1 + (population - t.livingFrom) / Math.Max(1, t.peoplePerLuxury));
    }

    /// <summary>
    /// This Seventh's luxuries: each category's want (per hundred citizens), taken from its resources in order as far as
    /// <paramref name="held"/> reaches; the score is the best-met categories, as many as are wanted, averaged. Nothing is
    /// taken while no category is wanted.
    /// </summary>
    public static LuxuryPlan Luxuries(CultureLifeTuning t, Func<string, float> held, int population,
        IEnumerable<CulturalAmenity> amenities = null, Func<string, bool> mayConsume = null)
    {
        t = t ?? new CultureLifeTuning();
        var plan = new LuxuryPlan { wanted = WantedLuxuries(population, t) };
        if (plan.wanted <= 0) return plan;
        var left = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var places = (amenities ?? Enumerable.Empty<CulturalAmenity>()).ToList();
        var remaining = t.luxuries.Where(l => l != null).ToList();
        LuxuryDraw Preview(LuxurySpec spec)
        {
            var draw = new LuxuryDraw { category = spec.category, want = Math.Max(0f, spec.perHundred) * population / 100f };
            float need = draw.want;
            foreach (var place in places.Where(p => p.category == spec.category))
            {
                float have = left.TryGetValue(place.source, out float available) ? available : Math.Max(0f, place.amount);
                float use = Math.Min(have, need);
                if (use <= 0f) continue;
                draw.enjoys.Add((place.source, use));
                draw.faith += use * Math.Max(0f, place.faithPerUnit);
                need -= use;
            }
            foreach (string resource in (spec.resources ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (need <= 1e-6f) break;
                if (string.IsNullOrEmpty(resource)) continue;
                var good = t.Good(resource);
                if (good?.amenity != true && mayConsume != null && !mayConsume(resource)) continue;
                if (!left.TryGetValue(resource, out float have)) have = Math.Max(0f, held?.Invoke(resource) ?? 0f);
                float take = Math.Min(have, need);
                if (take <= 0f) continue;
                if (good?.amenity == true) draw.enjoys.Add((resource, take));
                else draw.takes.Add((resource, take));
                draw.faith += take * Math.Max(0f, good?.faithPerUnit ?? 0f);
                need -= take;
            }
            return draw;
        }
        // Commit only the categories that count. Re-evaluate after every choice so shared stock cannot pay twice.
        while (remaining.Count > 0 && plan.draws.Count < plan.wanted)
        {
            var chosen = remaining.Select(spec => (spec, draw: Preview(spec))).OrderByDescending(p => p.draw.Met)
                .ThenBy(p => p.draw.Taken).First(); // on ties enjoy lasting amenities before spending stores
            remaining.Remove(chosen.spec);
            plan.draws.Add(chosen.draw);
            foreach (var use in chosen.draw.takes.Concat(chosen.draw.enjoys))
            {
                string source = use.Item1;
                if (!left.TryGetValue(source, out float have))
                    have = places.FirstOrDefault(p => p.source == source)?.amount ?? Math.Max(0f, held?.Invoke(source) ?? 0f);
                left[source] = Math.Max(0f, have - use.Item2);
            }
        }
        plan.score = plan.draws.Sum(d => d.Met) / plan.wanted;
        return plan;
    }

    /// <summary>Only surveyed, living sites on the player's land count; each patch counts once, scaled by its vigor.</summary>
    public static List<CulturalAmenity> Amenities(WorldMap map, WorldGenSettings settings, CultureLifeTuning life)
    {
        var result = new List<CulturalAmenity>();
        if (map == null || settings == null || life?.gardens == null) return result;
        foreach (var site in map.ResourceSites)
        {
            var garden = life.gardens.FirstOrDefault(g => g != null && g.site == site.spec);
            if (garden == null || !site.cells.Any(c => map[c].explored && WorldAuthority.IsPlayers(map[c].authorityId))) continue;
            float share = site.cells.Count(c => map[c].explored && WorldAuthority.IsPlayers(map[c].authorityId)) / (float)Math.Max(1, site.cells.Count);
            float amount = Math.Max(0f, garden.amenity) * WorldResources.Gift(site, settings) * share;
            if (amount > 0f) result.Add(new CulturalAmenity { source = "site:" + site.index, category = garden.category, amount = amount, faithPerUnit = garden.faithPerUnit });
        }
        return result;
    }

    public static string ResourceUse(CultureLifeTuning life, string resource)
    {
        var categories = life.luxuries.Where(l => l != null && l.resources.Contains(resource)).Select(l => l.category).ToList();
        if (categories.Count == 0) return null;
        var good = life.Good(resource);
        string use = good?.amenity == true ? "Lasting amenity; enjoyed without consuming it" : "Luxury; consumed when its category is chosen";
        return $"{use}: {string.Join(", ", categories)}." + ((good?.faithPerUnit ?? 0f) > 0f ? $" +{good.faithPerUnit:0.##} Faith per unit enjoyed each Seventh." : string.Empty);
    }

    // ===== THE KITCHEN =====

    /// <summary>Whole batches of <paramref name="recipe"/> the stores can make now (0 with an input missing).</summary>
    public static int Batches(RecipeSpec recipe, Func<string, float> held)
    {
        if (recipe == null || recipe.inputs == null || recipe.inputs.Count == 0) return 0;
        int batches = int.MaxValue;
        foreach (var input in recipe.inputs)
        {
            if (input == null || input.amount <= 0f) continue;
            float have = Math.Max(0f, held?.Invoke(input.resource) ?? 0f);
            batches = Math.Min(batches, (int)Math.Floor(have / input.amount + 1e-4f));
        }
        return batches == int.MaxValue ? 0 : Math.Max(0, batches);
    }

    /// <summary>Dishes one batch makes: its output, more while a civic of the kitchen is in force.</summary>
    public static float Yield(RecipeSpec recipe, bool kitchenCivic, float civicBonus) =>
        recipe == null ? 0f : Math.Max(0f, recipe.output) * (kitchenCivic ? 1f + Math.Max(0f, civicBonus) : 1f);

    /// <summary>Food value that goes into a batch and comes out of it (<paramref name="foodValue"/>: a resource's food value, 0 for spices).</summary>
    public static (float input, float output) FoodValue(RecipeSpec recipe, Func<string, float> foodValue, float yield)
    {
        if (recipe == null) return (0f, 0f);
        float input = (recipe.inputs ?? new List<ResourceAmount>()).Where(i => i != null).Sum(i => Math.Max(0f, i.amount) * Math.Max(0f, foodValue?.Invoke(i.resource) ?? 0f));
        return (input, Math.Max(0f, yield) * Math.Max(0f, foodValue?.Invoke(recipe.dish) ?? 0f));
    }

    // ===== HOLIDAYS =====

    /// <summary>Unity the next holiday costs: more for each already kept.</summary>
    public static float HolidayCost(int holidays, CultureLifeTuning t)
    {
        t = t ?? new CultureLifeTuning();
        return Math.Max(0f, t.holidayUnityCost + t.holidayUnityCostStep * Math.Max(0, holidays));
    }

    /// <summary>Why the people cannot set a day apart now, or null (the checks in the order the player meets them).</summary>
    public static string WhyNotHoliday(CultureLifeTuning t, bool founded, int festivalsHeld, int moraleSurplus, float unity, IList<Holiday> holidays,
        int seventhsSinceLast, int seventh, int phase)
    {
        t = t ?? new CultureLifeTuning();
        int count = holidays?.Count ?? 0;
        if (!founded) return "The culture has not been founded yet.";
        if (count >= Math.Max(0, t.maxHolidays)) return $"The calendar already keeps {count} holidays; a people can only stop working so often.";
        if (t.holidayNeedsFestival && festivalsHeld <= 0) return "Hold a festival first: a people sets a day apart only once it has learned to celebrate (a cultural party holds one in a settlement).";
        if (count > 0 && seventhsSinceLast < t.holidayCooldownSevenths) return $"The last holiday is still new: {t.holidayCooldownSevenths - seventhsSinceLast} more Sevenths.";
        if (holidays != null && holidays.Any(h => h != null && h.seventh == seventh && h.phase == phase)) return "This day is already a holiday.";
        if (moraleSurplus < t.holidayMoraleMargin) return $"Morale must stand {t.holidayMoraleMargin} above its balance (now {moraleSurplus:+0;-0;0}).";
        float cost = HolidayCost(count, t);
        if (unity + 1e-4f < cost) return $"Needs {cost:0} Unity ({unity:0} held).";
        return null;
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
