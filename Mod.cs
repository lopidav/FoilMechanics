using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FoilMechanicsNS
{
    public class FoilMechanics : Mod
    {
		
		public static FoilMechanics? _instance;
		public void Awake()
		{
			_instance = this;		
			try
			{
				//HarmonyInstance = new Harmony("FoilMechanics");
				Harmony.PatchAll(typeof(FoilMechanics));
				// Harmony.Patch((MethodBase)AccessTools.Method(typeof(WorldManager).GetNestedType("<KillVillagerCoroutine>d__231", BindingFlags.NonPublic), "MoveNext")
				// 	, new HarmonyMethod(AccessTools.Method(typeof(FoilMechanics), nameof(FoilMechanics.JustThisCardInvolved_Prefix)))
				// 	, new HarmonyMethod(AccessTools.Method(typeof(FoilMechanics), nameof(FoilMechanics.UniversalPostfix))));
			}
			catch(Exception e)
			{
				Logger.Log("Patching failed: " + e.Message);
			}
		}

		override public void Ready()
		{
		}
		public static void Log(string s)
		{
			if (_instance != null) _instance.Logger.Log(s);
		}
		public static void LogError(string s)
		{
			if (_instance != null) _instance.Logger.LogError(s);
		}
		private void OnDestroy()
        {
            Harmony.UnpatchSelf();
        }
		
		public void Update()
		{
			
			if ((InputController.instance.GetKey(UnityEngine.InputSystem.Key.LeftShift)
					|| InputController.instance.GetKey(UnityEngine.InputSystem.Key.RightShift))
				&& InputController.instance.GetKeyDown(UnityEngine.InputSystem.Key.F)
				&& WorldManager.instance.HoveredCard?.CardData != null)
			{
				if (!WorldManager.instance.HoveredCard.CardData.IsFoil)
				{
					WorldManager.instance.HoveredCard.CardData.SetFoil();
				}
				else
				{
					WorldManager.instance.HoveredCard.CardData.IsFoil = false;
					if (WorldManager.instance.HoveredCard.CardData.Value > 0) WorldManager.instance.HoveredCard.CardData.Value /= 5;
				}
			}
		}

		public struct FoilChancesState
		{
			public float chance = 0;
			public float hightenedChance = 0;
			public int hightenedChanceCardCount = 0;
			public GameCard? lastCardBeforeCreation = null;
			public int allCardsCountBeforeCreation;
			public List<GameCard> involvedCards = new List<GameCard>();
            public FoilChancesState()
            {
            }
			public void CalculateFromInvolvedCards()
			{
				var involvedGameCards = this.involvedCards;
				this.chance = this.involvedCards.Count == 0 ? 0 : (float)this.involvedCards.Count(card => card.CardData != null && card.CardData.IsFoil) / (float)this.involvedCards.Count;
				this.lastCardBeforeCreation = WorldManager.instance.AllCards.LastOrDefault(card => card.CardData == null || !involvedGameCards.Contains(card));
				this.allCardsCountBeforeCreation = WorldManager.instance.AllCards.Count;
			}
        }


		[HarmonyPatch(typeof(WorldManager), nameof(WorldManager.ChangeToCard))]
		[HarmonyPrefix]
		public static void OneToOneTransform_Prefix(GameCard card, out bool __state)
		{
			__state = card.CardData.IsFoil;
		}
		[HarmonyPatch(typeof(WorldManager), nameof(WorldManager.ChangeToCard))]
		[HarmonyPostfix]
		public static void OneToOneTransform_Postfix(GameCard card, ref bool __state)
		{
			if (!__state) return;
			if (!card.CardData.IsFoil) card.CardData.SetFoil();
		}

		[HarmonyPatch(typeof(Blueprint), nameof(Blueprint.BlueprintComplete))]
		[HarmonyPatch(typeof(BlueprintOffspring), nameof(BlueprintOffspring.BlueprintComplete))]
		[HarmonyPatch(typeof(BlueprintFillBottle), nameof(BlueprintFillBottle.BlueprintComplete))]
		[HarmonyPrefix]
		public static void Blueprint_BlueprintComplete_Prefix(GameCard rootCard, List<GameCard> involvedCards, Subprint print, out FoilChancesState __state)
		{
			__state = new FoilChancesState();
			__state.involvedCards = involvedCards;
			__state.CalculateFromInvolvedCards();
		}
		
		[HarmonyPatch(typeof(Bone), nameof(Bone.StoppedDragging))]
		[HarmonyPrefix]
		public static void Bone_StoppedDragging_Prefix(CardData __instance, out FoilChancesState __state)
		{
			__state = new FoilChancesState();
			if (!__instance.MyGameCard.HasParent || __instance.MyGameCard.Parent.CardData.Id != "wolf") return;
			__state.involvedCards = new List<GameCard>(){__instance.MyGameCard, __instance.MyGameCard.Parent};
			__state.CalculateFromInvolvedCards();
		}
		[HarmonyPatch(typeof(Milk), nameof(Milk.StoppedDragging))]
		[HarmonyPrefix]
		public static void Milk_StoppedDragging_Prefix(CardData __instance, out FoilChancesState __state)
		{
			__state = new FoilChancesState();
			if (!__instance.MyGameCard.HasParent || __instance.MyGameCard.Parent.CardData.Id != "feral_cat") {return;}
			__state.involvedCards = new List<GameCard>(){__instance.MyGameCard, __instance.MyGameCard.Parent};
			__state.CalculateFromInvolvedCards();
		}
		[HarmonyPatch(typeof(TreasureChest), nameof(TreasureChest.UpdateCard))]
		[HarmonyPrefix]
		public static void TreasureChest_UpdateCard_Prefix(CardData __instance, out FoilChancesState __state)
		{
			__state = new FoilChancesState();
			__instance.HasCardOnTop("key", out var card);
			if (card == null) return;
			__state.involvedCards = new List<GameCard>(){card.MyGameCard, __instance.MyGameCard};
			__state.CalculateFromInvolvedCards();
		}

		[HarmonyPatch(typeof(Brickyard), nameof(Brickyard.CompleteMaking))]
		[HarmonyPrefix]
		public static void Brickyard_CompleteMaking_Prefix(CardData __instance, out FoilChancesState __state)
		{
			__state = new FoilChancesState();
            List<CardData> involvedCards = new List<CardData>();
            __instance.MyGameCard.GetRootCard().CardData.GetChildrenMatchingPredicate((CardData c) => c.Id == "stone" || c.Id == "sandstone", involvedCards);
			involvedCards = involvedCards.GetRange(0, 2);
			involvedCards.Add(__instance);
			__state.involvedCards = involvedCards.Select(card => card.MyGameCard).ToList();
			
			__state.CalculateFromInvolvedCards();
		}
		[HarmonyPatch(typeof(Composter), nameof(Composter.Compost))]
		[HarmonyPrefix]
		public static void Composter_Compost_Prefix(CardData __instance, out FoilChancesState __state)
		{
			__state = new FoilChancesState();
            List<CardData> involvedCards = new List<CardData>();
            __instance.MyGameCard.GetRootCard().CardData.GetChildrenMatchingPredicate(card => card.MyCardType == CardType.Food, involvedCards);
			involvedCards = involvedCards.GetRange(0, 5);
			involvedCards.Add(__instance);
			__state.involvedCards = involvedCards.Select(card => card.MyGameCard).ToList();
			__state.CalculateFromInvolvedCards();
		}
		[HarmonyPatch(typeof(House), nameof(House.GrowUpKid))]
		[HarmonyPrefix]
		public static void House_GrowUpKid_Prefix(CardData __instance, out FoilChancesState __state)
		{
			__state = new FoilChancesState();
			__instance.HasCardOnTop(out Kid card);
			__state.involvedCards = new List<GameCard>() {card.MyGameCard};
			__state.CalculateFromInvolvedCards();
		}
		[HarmonyPatch(typeof(Combatable), nameof(Combatable.CheckDeath))]
		[HarmonyPrefix]
		public static void Combatable_CheckDeath_Prefix(Combatable __instance, out FoilChancesState __state)
		{
			__state = new FoilChancesState();
			// if (__instance.InConflict) {
			// 	__state.involvedCards = __instance.MyConflict.Participants.Select(combatable => combatable.MyGameCard).ToList();
			// }
			// else
			{
				__state.involvedCards = new List<GameCard>() {__instance.MyGameCard};
			}
			__state.CalculateFromInvolvedCards();
		}
		[HarmonyPatch(typeof(Enemy), nameof(Enemy.UpdateCard))]
		[HarmonyPrefix]
		public static void Enemy_UpdateCard_Prefix(CardData __instance, out FoilChancesState __state)
		{
			__state = new FoilChancesState();
			CardData? card = null;
			if (__instance.Id == "wolf") __instance.HasCardOnTop("bone", out card);
			else if (__instance.Id == "feral_cat") __instance.HasCardOnTop("milk", out card);
			if (card == null) return;
			__state.involvedCards = new List<GameCard>() {card.MyGameCard, __instance.MyGameCard};
			__state.CalculateFromInvolvedCards();
		}
		[HarmonyPatch(typeof(Combatable), nameof(Combatable.PerformAttack))]
		[HarmonyPrefix]
		public static void Killing_WithFoils_Prefix(Combatable target, Combatable __instance, out FoilChancesState __state)
		{
			__state = new FoilChancesState();
			if (__instance.MyGameCard != null) {
				__state.involvedCards.Add(__instance.MyGameCard);
				__state.involvedCards.AddRange(__instance.GetAllEquipables().Select(e => e.MyGameCard));
			}
			if (target.MyGameCard != null) __state.involvedCards.Add(target.MyGameCard);
			__state.CalculateFromInvolvedCards();
			// Log(__state.involvedCards.Count.ToString());
			// Log(__state.chance.ToString());
			// __state.involvedCards.ForEach(c=>Log(c.CardData.Id));
			// __state.involvedCards.ForEach(c=>Log(c.CardData.IsFoil.ToString()));
			// Log( __state.involvedCards.Count(card => card.CardData != null && card.CardData.IsFoil).ToString());
			// Log((__state.involvedCards.Count == 0 ? 0 : (float)__state.involvedCards.Count(card => card.CardData != null && card.CardData.IsFoil) / (float)__state.involvedCards.Count).ToString());
		}

		[HarmonyPatch(typeof(AngryRoyal), nameof(AngryRoyal.DieInCutscene))]
		[HarmonyPatch(typeof(CardData), nameof(CardData.ParseAction))]
		[HarmonyPatch(typeof(Animal), nameof(Animal.TryCreateItem))]
		[HarmonyPatch(typeof(PirateBoat), nameof(PirateBoat.SpawnPirates))]
		[HarmonyPatch(typeof(StrangePortal), nameof(StrangePortal.SpawnCreature))]
		// [HarmonyPatch(typeof(Mob), nameof(Mob.Die))]
		[HarmonyPrefix]
		public static void JustThisCardInvolved_Prefix(CardData __instance, out FoilChancesState __state)
		{
			__state = new FoilChancesState();
			if (!__instance.IsFoil || __instance.MyGameCard == null) {Log("No foils");return;}
			__state.involvedCards = new List<GameCard>() {__instance.MyGameCard};
			__state.CalculateFromInvolvedCards();
		}
		[HarmonyPatch(typeof(CombatableHarvestable), nameof(CombatableHarvestable.CompleteHarvest))]
		[HarmonyPatch(typeof(Harvestable), nameof(Harvestable.CompleteHarvest))]
		[HarmonyPatch(typeof(BreedingPen), nameof(BreedingPen.BreedAnimals))]
		[HarmonyPrefix]
		public static void AllChildrenInvolved_Prefix(CardData __instance, out FoilChancesState __state)
		{
			__state = new FoilChancesState();
			if (__instance.MyGameCard == null) return;
			var involvedCards = __instance.MyGameCard.GetChildCards();
			involvedCards.Add(__instance.MyGameCard);
			__state.involvedCards = involvedCards;
			__state.CalculateFromInvolvedCards();
		}

		[HarmonyPatch(typeof(Blueprint), nameof(Blueprint.BlueprintComplete))]
		[HarmonyPatch(typeof(BlueprintOffspring), nameof(BlueprintOffspring.BlueprintComplete))]
		[HarmonyPatch(typeof(BlueprintFillBottle), nameof(BlueprintFillBottle.BlueprintComplete))]
		[HarmonyPatch(typeof(Brickyard), nameof(Brickyard.CompleteMaking))]
		[HarmonyPatch(typeof(BreedingPen), nameof(BreedingPen.BreedAnimals))]
		[HarmonyPatch(typeof(Composter), nameof(Composter.Compost))]
		[HarmonyPatch(typeof(House), nameof(House.GrowUpKid))]
		[HarmonyPatch(typeof(Enemy), nameof(Enemy.UpdateCard))]
		[HarmonyPatch(typeof(CombatableHarvestable), nameof(CombatableHarvestable.CompleteHarvest))]
		[HarmonyPatch(typeof(Harvestable), nameof(Harvestable.CompleteHarvest))]
		[HarmonyPatch(typeof(Bone), nameof(Bone.StoppedDragging))]
		[HarmonyPatch(typeof(Milk), nameof(Milk.StoppedDragging))]
		[HarmonyPatch(typeof(AngryRoyal), nameof(AngryRoyal.DieInCutscene))]
		[HarmonyPatch(typeof(CardData), nameof(CardData.ParseAction))]
		[HarmonyPatch(typeof(Animal), nameof(Animal.TryCreateItem))]
		[HarmonyPatch(typeof(PirateBoat), nameof(PirateBoat.SpawnPirates))]
		[HarmonyPatch(typeof(StrangePortal), nameof(StrangePortal.SpawnCreature))]
		[HarmonyPatch(typeof(TreasureChest), nameof(TreasureChest.UpdateCard))]
		[HarmonyPatch(typeof(Combatable), nameof(Combatable.CheckDeath))]
		// [HarmonyPatch(typeof(Mob), nameof(Mob.Die))]
		[HarmonyPatch(typeof(Combatable), nameof(Combatable.PerformAttack))]
		[HarmonyPostfix]
		public static void UniversalPostfix(ref FoilChancesState __state)
		{
			if (__state.involvedCards.Count == 0) return;
			__state.chance *= 1f; //TODO - cahnge to .01
			int destroyedFoils = 0;
			int destroyedNonfoils = 0;
			foreach (var card in __state.involvedCards) {
				if (card.Destroyed) {

					if (card?.CardData?.IsFoil ?? false) destroyedFoils++;
					else destroyedNonfoils++;
				} 
			}
			__state.hightenedChance = (destroyedFoils + destroyedNonfoils);
			if (__state.hightenedChance != 0) {
				__state.hightenedChance = destroyedFoils / __state.hightenedChance;
				__state.hightenedChanceCardCount = destroyedFoils;
			}
			Log("Foil postfix:");
					Log(__state.hightenedChance.ToString());
					Log(__state.hightenedChanceCardCount.ToString());
					Log(__state.chance.ToString());
			if (__state.chance == 0f && __state.hightenedChance == 0f) return;
			if (__state.lastCardBeforeCreation == null) __state.lastCardBeforeCreation = WorldManager.instance.AllCards[Math.Min(__state.allCardsCountBeforeCreation, WorldManager.instance.AllCards.Count)];
			foreach (var card in WorldManager.instance.AllCards)
			{
				if (__state.lastCardBeforeCreation != null)
				{
					if (card == __state.lastCardBeforeCreation) __state.lastCardBeforeCreation = null; 
					continue;
				}
				Log("New card found:" + card.CardData?.Id);
				if (card.CardData != null && !__state.involvedCards.Contains(card) && ((__state.hightenedChanceCardCount > 0 && UnityEngine.Random.Range(0f, 1f) <= __state.hightenedChance) || UnityEngine.Random.Range(0f, 1f) <= __state.chance))
				{
			Log("turning card foil:");
					Log(card.CardData.Id);
					Log(__state.hightenedChance.ToString());
					Log(__state.hightenedChanceCardCount.ToString());
					Log(__state.chance.ToString());
					if (!card.CardData.IsFoil) card.CardData.SetFoil();
					__state.hightenedChanceCardCount--;
				}
			}
		}

		[HarmonyPatch(typeof(WorldManager), nameof(WorldManager.KillVillagerCoroutine))]
		[HarmonyPostfix]
		public static void UniversalIterablePostfix(Combatable combatable, Action onComplete, ref Action onCreateCorpse, ref IEnumerator __result)
		{
			var __state1 = new FoilChancesState();
				
			Action prefixAction = () => {};
			Action postfixAction = () => {};
			Action<object> preItemAction = (item) => {
				if (__state1.involvedCards.Count != 0) {
					UniversalPostfix(ref __state1);
				}
			};
			Action<object> postItemAction = (item) => {
				if (combatable.MyGameCard != null) {
					__state1.involvedCards = new List<GameCard>() {combatable.MyGameCard};
					__state1.CalculateFromInvolvedCards();
					combatable.GetAllEquipables().ForEach(equip => {combatable.MyGameCard.Unequip(equip); equip.MyGameCard.SendIt();});
				}
			};
			Func<object, object> itemAction = (item) => {return item;};
			var myEnumerator = new SimpleEnumerator()
			{
				enumerator = __result,
				prefixAction = prefixAction,
				postfixAction = postfixAction,
				preItemAction = preItemAction,
				postItemAction = postItemAction,
				itemAction = itemAction
			};
			__result = myEnumerator.GetEnumerator();
		}
		class SimpleEnumerator : IEnumerable
		{
			public IEnumerator enumerator;
			public Action prefixAction, postfixAction;
			public Action<object> preItemAction, postItemAction;
			public Func<object, object> itemAction;
			IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
			public IEnumerator GetEnumerator()
			{
				prefixAction();
				while (enumerator.MoveNext())
				{
					var item = enumerator.Current;
					preItemAction(item);
					yield return itemAction(item);
					postItemAction(item);
				}
				postfixAction();
			}
		}

		// TODO: foil momma crab machanics Crab.Die
		// TODO: Merchant.UpdateCard
		// TODO: DemandManager.SpawnEnemies
		// TODO: ForestCombatManager
		// TODO: EndOfMonthCutscenes.UseHappiness
		// TODO: University
		// TODO: Chests
	}
	
}