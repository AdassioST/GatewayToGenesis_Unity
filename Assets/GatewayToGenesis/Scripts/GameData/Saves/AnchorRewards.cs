using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Gateway to Genesis/Anchor Rewards")]
public sealed class AnchorRewards : ScriptableObject
{
    [Min(0)] public int defaultAmount = 10;
    public System.Collections.Generic.List<AnchorAward> overrides = new System.Collections.Generic.List<AnchorAward>();
    public int Amount(string id) => Math.Max(0, overrides.FirstOrDefault(a => a.achievement == id)?.amount ?? defaultAmount);
}
