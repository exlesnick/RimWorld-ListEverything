﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	class ListFilterSkill : ListFilterDropDown<SkillDef>
	{
		IntRange skillRange = new IntRange(10, 20);

		public ListFilterSkill()
		{
			sel = SkillDefOf.Animals;
			drawStyle = DropDownDrawStyle.OptionsAndDrawSpecial;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref skillRange, "skillRange");
		}
		public override ListFilter Clone(Map map, FindDescription newOwner)
		{
			ListFilterSkill clone = (ListFilterSkill)base.Clone(map, newOwner);
			clone.skillRange = skillRange;
			return clone;
		}

		public override bool FilterApplies(Thing thing) =>
			thing is Pawn pawn &&
				pawn.skills?.GetSkill(sel) is SkillRecord rec &&
				!rec.TotallyDisabled &&
				skillRange.Includes(rec.Level);
		
		public override bool DrawSpecial(Rect rect, WidgetRow row)
		{
			IntRange newRange = skillRange;
			Widgets.IntRange(rect, id, ref newRange, SkillRecord.MinLevel, SkillRecord.MaxLevel);
			if (newRange != skillRange)
			{
				skillRange = newRange;
				return true;
			}
			return false;
		}
	}

	class ListFilterTrait : ListFilterDropDown<TraitDef>
	{
		int traitDegree = TraitDefOf.Beauty.degreeDatas.First().degree;
		public ListFilterTrait()
		{
			sel = TraitDefOf.Beauty;  //Todo: beauty shows even if it's not on map
			drawStyle = DropDownDrawStyle.OptionsAndDrawSpecial;
		}
		public override string NameFor(TraitDef def) =>
			def.degreeDatas.Count == 1
				? def.degreeDatas.First().label.CapitalizeFirst()
				: def.defName + "*";//TraitDefs don't have labels

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref traitDegree, "traitDegree");
		}
		public override ListFilter Clone(Map map, FindDescription newOwner)
		{
			ListFilterTrait clone = (ListFilterTrait)base.Clone(map, newOwner);
			clone.traitDegree = traitDegree;
			return clone;
		}

		public override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return pawn.story?.traits.GetTrait(sel) is Trait trait &&
				trait.Degree == traitDegree;
		}

		public override IEnumerable<TraitDef> Options() =>
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableInGame(t => (t as Pawn)?.story?.traits.allTraits.Select(tr => tr.def) ?? Enumerable.Empty<TraitDef>())
				: base.Options();

		public override bool Ordered => true;
		protected override void Callback(TraitDef o)
		{
			sel = o;
			traitDegree = sel.degreeDatas.First().degree;
		}

		public override bool DrawSpecial(Rect rect, WidgetRow row)
		{
			if (sel.degreeDatas.Count > 1 &&
				row.ButtonText(sel.DataAtDegree(traitDegree).label.CapitalizeFirst()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (TraitDegreeData deg in sel.degreeDatas)
				{
					options.Add(new FloatMenuOption(deg.label.CapitalizeFirst(), () => traitDegree = deg.degree));
				}
				MainTabWindow_List.DoFloatMenu(options);
			}
			return false;
		}
	}

	class ListFilterThought: ListFilterDropDown<ThoughtDef>
	{
		IntRange stageRange;
		public ListFilterThought()
		{
			sel = ThoughtDefOf.AteWithoutTable;
			drawStyle = DropDownDrawStyle.OptionsAndDrawSpecial;
		}
		public override string NameFor(ThoughtDef def)
		{
			string label =
				def.label?.CapitalizeFirst() ??
				def.stages.FirstOrDefault(d => d?.label != null).label.CapitalizeFirst() ??
				def.stages.FirstOrDefault(d => d?.labelSocial != null).labelSocial.CapitalizeFirst() ?? "???";

			return def.stages.Count > 1 ? label + "*" : label;
		}
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref stageRange, "stageRange");
		}
		public override ListFilter Clone(Map map, FindDescription newOwner)
		{
			ListFilterThought clone = (ListFilterThought)base.Clone(map, newOwner);
			clone.stageRange = stageRange;
			return clone;
		}

		public override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			if (pawn.needs?.TryGetNeed<Need_Mood>() is Need_Mood mood)
			{
				//memories
				if (mood.thoughts.memories.Memories.Any(t => t.def == sel && stageRange.Includes(t.CurStageIndex)))
					return true;

				//situational
				List<Thought> thoughts = new List<Thought>();
				mood.thoughts.situational.AppendMoodThoughts(thoughts);
				if (thoughts.Any(t => t.def == sel && stageRange.Includes(t.CurStageIndex)))
					return true;
			}
			return false;
		}

		public override IEnumerable<ThoughtDef> Options() =>
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableInGame(ThoughtsForThing)
				: base.Options();
		public override bool Ordered => true;
		protected override void Callback(ThoughtDef o)
		{
			sel = o;
			stageRange = new IntRange(0, 0);
		}
		public override bool DrawSpecial(Rect rect, WidgetRow row) => false;//Too big for one line

		public override bool DrawMore(Listing_StandardIndent listing)
		{
			if (sel.stages.Count <= 1) return false;

			//Buttons apparently are too tall for the line height?
			listing.Gap(listing.verticalSpacing);

			Rect nextRect = listing.GetRect(Text.LineHeight);

			WidgetRow row = new WidgetRow(nextRect.x, nextRect.y);
			//Actually Range from 1 to 2 is fine cause it can match both 
			//if(sel.stages.Count == 2)
			//	DoStageDropdown(row, stageRange.min, i => { stageRange.min = i; stageRange.max = i; });
			//else
			{
				row.Label("TD.From".Translate());
				DoStageDropdown(row, stageRange.min, i => stageRange.min = i);
				row.Label("RangeTo".Translate());
				DoStageDropdown(row, stageRange.max, i => stageRange.max = i);
			}
			return false;
		}

		private void DoStageDropdown(WidgetRow row, int setI, Action<int> selectedAction)
		{
			if (row.ButtonText(sel.stages[setI]?.label.CapitalizeFirst() ?? "TD.Invisible".Translate()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				IEnumerable<int> stageIndices = ContentsUtility.onlyAvailable ?
					ContentsUtility.AvailableInGame(t => ThoughtStagesForThing(t, sel)) :
					Enumerable.Range(0, sel.stages.Count);
				foreach (int i in stageIndices.Where(i => DebugSettings.godMode || (sel.stages[i]?.visible ?? false)))
				{
					int localI = i;
					options.Add(new FloatMenuOption(sel.stages[i]?.label.CapitalizeFirst() ?? "TD.Invisible".Translate(), () => selectedAction(localI)));
				}
				MainTabWindow_List.DoFloatMenu(options);
			}
		}

		public static IEnumerable<ThoughtDef> ThoughtsForThing(Thing t)
		{
			Pawn pawn = t as Pawn;
			if (pawn == null) yield break;

			IEnumerable<ThoughtDef> memories = pawn.needs?.TryGetNeed<Need_Mood>()?.thoughts.memories.Memories.Where(th => th.CurStage.visible).Select(th => th.def);
			if (memories != null)
				foreach (ThoughtDef def in memories)
					yield return def;

			List<Thought> thoughts = new List<Thought>();
			pawn.needs?.TryGetNeed<Need_Mood>()?.thoughts.situational.AppendMoodThoughts(thoughts);
			foreach (Thought thought in thoughts)
				yield return thought.def;
		}

		public static IEnumerable<int> ThoughtStagesForThing(Thing t, ThoughtDef def)
		{
			Pawn pawn = t as Pawn;
			if (pawn == null) yield break;

			IEnumerable<int> stages = pawn.needs?.TryGetNeed<Need_Mood>()?.thoughts.memories.Memories.Where(th => th.def == def && th.CurStage.visible).Select(th => th.CurStageIndex);
			if (stages != null)
				foreach (int stage in stages)
					yield return stage;

			List<Thought> thoughts = new List<Thought>();
			pawn.needs?.TryGetNeed<Need_Mood>()?.thoughts.situational.AppendMoodThoughts(thoughts);
			foreach (Thought thought in thoughts)
				if (thought.def == def)
					yield return thought.CurStageIndex;
		}
	}

	class ListFilterNeed : ListFilterDropDown<NeedDef>
	{
		FloatRange needRange = new FloatRange(0, 0.5f);
		public ListFilterNeed()
		{
			sel = NeedDefOf.Food;
			drawStyle = DropDownDrawStyle.OptionsAndDrawSpecial;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref needRange, "needRange");
		}
		public override ListFilter Clone(Map map, FindDescription newOwner)
		{
			ListFilterNeed clone = (ListFilterNeed)base.Clone(map, newOwner);
			clone.needRange = needRange;
			return clone;
		}

		public override bool FilterApplies(Thing thing) =>
			thing is Pawn pawn &&
			(!pawn.RaceProps.Animal || pawn.Faction != null || DebugSettings.godMode) &&
				pawn.needs?.TryGetNeed(sel) is Need need && needRange.Includes(need.CurLevelPercentage);

		public override bool DrawSpecial(Rect rect, WidgetRow row)
		{
			FloatRange newRange = needRange;
			Widgets.FloatRange(rect, id, ref newRange, valueStyle: ToStringStyle.PercentOne);
			if (newRange != needRange)
			{
				needRange = newRange;
				return true;
			}
			return false;
		}
	}

	class ListFilterHealth : ListFilterDropDown<HediffDef>
	{
		FloatRange? severityRange;

		public ListFilterHealth()
		{
			drawStyle = DropDownDrawStyle.OptionsAndDrawSpecial;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref severityRange, "severityRange");
		}
		public override ListFilter Clone(Map map, FindDescription newOwner)
		{
			ListFilterHealth clone = (ListFilterHealth)base.Clone(map, newOwner);
			clone.severityRange = severityRange;
			return clone;
		}

		public override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return
				extraOption == 1 ? pawn.health.hediffSet.hediffs.Any(h => h.Visible || DebugSettings.godMode) :
				sel == null ? !pawn.health.hediffSet.hediffs.Any(h => h.Visible || DebugSettings.godMode) :
				(pawn.health.hediffSet.GetFirstHediffOfDef(sel, !DebugSettings.godMode) is Hediff hediff &&
				(!severityRange.HasValue || severityRange.Value.Includes(hediff.Severity)));
		}

		public override string NullOption() => "None".Translate();
		public override IEnumerable<HediffDef> Options() =>
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableInGame(t => (t as Pawn)?.health.hediffSet.hediffs.Select(h => h.def) ?? Enumerable.Empty<HediffDef>())
				: base.Options();
		public override bool Ordered => true;
		protected override void Callback(HediffDef o)
		{
			sel = o;
			severityRange = SeverityRangeFor(sel);
		}

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();

		public override bool DrawSpecial(Rect rect, WidgetRow row)
		{
			if (sel != null && severityRange.HasValue)
			{
				Rect rangeRect = rect;
				rangeRect.xMin = row.FinalX;
				FloatRange newRange = severityRange.Value;
				FloatRange boundRange = SeverityRangeFor(sel).Value;
				Widgets.FloatRange(rangeRect, id, ref newRange, boundRange.min, boundRange.max, valueStyle: ToStringStyle.FloatOne);
				if (newRange != severityRange.Value)
				{
					severityRange = newRange;
					return true;
				}
			}
			return false;
		}

		public static FloatRange? SeverityRangeFor(HediffDef hediffDef)
		{
			if (hediffDef == null) return null;
			float min = hediffDef.minSeverity;
			float max = hediffDef.maxSeverity;
			if (hediffDef.lethalSeverity != -1f)
				max = Math.Min(max, hediffDef.lethalSeverity);

			if (max == float.MaxValue) return null;
			return new FloatRange(min, max);
		}
	}

	class ListFilterIncapable : ListFilterDropDown<WorkTags>
	{
		public override string NameFor(WorkTags tags) =>
			tags.LabelTranslated().CapitalizeFirst();

		public override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			Pawn_StoryTracker story = pawn.story;
			if (story == null) return false;

			return 
				extraOption == 1 ? story.CombinedDisabledWorkTags != WorkTags.None :
				sel == WorkTags.None ? story.CombinedDisabledWorkTags == WorkTags.None :
				story.WorkTagIsDisabled(sel);
		}

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
	}

	enum TemperatureFilter { Cold, Cool, Okay, Warm, Hot }
	class ListFilterTemp : ListFilterDropDown<TemperatureFilter>
	{
		public override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;
			float temp = pawn.AmbientTemperature;
			FloatRange safeRange = pawn.SafeTemperatureRange();
			FloatRange comfRange = pawn.ComfortableTemperatureRange();
			switch (sel)
			{
				case TemperatureFilter.Cold: return temp < safeRange.min;
				case TemperatureFilter.Cool: return temp >= safeRange.min && temp < comfRange.min;
				case TemperatureFilter.Okay: return comfRange.Includes(temp);
				case TemperatureFilter.Warm: return temp <= safeRange.max && temp > comfRange.max;
				case TemperatureFilter.Hot: return temp > safeRange.max;
			}
			return false;//???
		}
		public override string NameFor(TemperatureFilter o)
		{
			switch (o)
			{
				case TemperatureFilter.Cold: return "TD.Cold".Translate();
				case TemperatureFilter.Cool: return "TD.ALittleCold".Translate();
				case TemperatureFilter.Okay: return "TD.Comfortable".Translate();
				case TemperatureFilter.Warm: return "TD.ALittleHot".Translate();
				case TemperatureFilter.Hot: return "TD.Hot".Translate();
			}
			return "???";
		}
	}

	class ListFilterRestricted : ListFilterDropDown<Area>
	{
		public override void ResolveReference(string refName, Map map) =>
			sel = map.areaManager.GetLabeled(refName);

		public override bool ValidForAllMaps => extraOption > 0 || sel == null;

		public override bool FilterApplies(Thing thing)
		{
			Area selectedArea = extraOption == 1 ? thing.MapHeld.areaManager.Home : sel;
			return thing is Pawn pawn && pawn.playerSettings is Pawn_PlayerSettings set && set.AreaRestriction == selectedArea;
		}

		public override string NullOption() => "NoAreaAllowed".Translate();
		public override IEnumerable<Area> Options() => Find.CurrentMap.areaManager.AllAreas.Where(a => a is Area_Allowed);//a.AssignableAsAllowed());
		public override string NameFor(Area o) => o.Label;

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "Home".Translate();
	}

	class ListFilterMentalState : ListFilterDropDown<MentalStateDef>
	{
		public override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return
				extraOption == 1 ? pawn.MentalState != null: 
				sel == null ? pawn.MentalState == null : 
				pawn.MentalState?.def is MentalStateDef def && def == sel;
		}

		public override IEnumerable<MentalStateDef> Options() =>
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableInGame(t => (t as Pawn)?.MentalState?.def)
				: base.Options();
		public override bool Ordered => true;
		public override string NullOption() => "None".Translate();

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
	}

	class ListFilterPrisoner : ListFilterDropDown<PrisonerInteractionModeDef>
	{
		public ListFilterPrisoner() => sel = PrisonerInteractionModeDefOf.NoInteraction;

		public override bool FilterApplies(Thing thing)
		{
			if (extraOption == 2)
				return thing.GetRoom()?.isPrisonCell ?? false;

			Pawn pawn = thing as Pawn;
			//Default setting for interactionMode is NoInteraction so fail early if not prisoner
			//this also covers extraOption == 1, isPrisoner
			if (!pawn?.IsPrisoner ?? true)
				return false;

			return pawn.guest?.interactionMode == sel;
		}
		
		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.IsPrisoner".Translate() : "TD.InCell".Translate();
	}

	enum DraftFilter { Drafted, Undrafted, Controllable }
	class ListFilterDrafted : ListFilterDropDown<DraftFilter>
	{
		public override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			switch (sel)
			{
				case DraftFilter.Drafted: return pawn.Drafted;
				case DraftFilter.Undrafted: return pawn.drafter != null && !pawn.Drafted;
				case DraftFilter.Controllable: return pawn.drafter != null;
			}
			return false;
		}
	}

	class ListFilterJob : ListFilterDropDown<JobDef>
	{
		public override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return pawn.CurJobDef == sel;
		}

		public override string NameFor(JobDef o) =>
			Regex.Replace(o.reportString.Replace(".",""), "Target(A|B|C)", "...");
		public override string NullOption() => "None".Translate();

		public override IEnumerable<JobDef> Options() =>
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableInGame(t => (t as Pawn)?.CurJobDef)
			: base.Options();
		public override bool Ordered => true;
	}

	enum RacePropsFilter { Predator, Prey, Herd, Pack, Wildness, Petness, Trainability, Intelligence }
	class ListFilterRaceProps : ListFilterDropDown<RacePropsFilter>
	{
		Intelligence intelligence;
		FloatRange wild;
		FloatRange petness;
		TrainabilityDef trainability;

		public ListFilterRaceProps()
		{
			drawStyle = DropDownDrawStyle.OptionsAndDrawSpecial;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref intelligence, "intelligence");
			Scribe_Values.Look(ref wild, "wild");
			Scribe_Values.Look(ref petness, "petness");
			Scribe_Defs.Look(ref trainability, "trainability");
		}
		public override ListFilter Clone(Map map, FindDescription newOwner)
		{
			ListFilterRaceProps clone = (ListFilterRaceProps)base.Clone(map, newOwner);
			clone.intelligence = intelligence;
			clone.wild = wild;
			clone.petness = petness;
			clone.trainability = trainability;
			return clone;
		}

		public override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			RaceProperties props = pawn.RaceProps;
			if (props == null) return false;

			switch (sel)
			{
				case RacePropsFilter.Intelligence: return props.intelligence == intelligence;
				case RacePropsFilter.Herd: 
					return props.herdAnimal;
				case RacePropsFilter.Pack: 
					return props.packAnimal;
				case RacePropsFilter.Predator: 
					return props.predator;
				case RacePropsFilter.Prey: 
					return props.canBePredatorPrey;
				case RacePropsFilter.Wildness: 
					return wild.Includes(props.wildness);
				case RacePropsFilter.Petness: 
					return petness.Includes(props.petness);
				case RacePropsFilter.Trainability:
					return props.trainability == trainability;
			}
			return false;
		}

		protected override void Callback(RacePropsFilter o)
		{
			sel = o;
			switch (sel)
			{
				case RacePropsFilter.Intelligence: intelligence = Intelligence.Humanlike; return;
				case RacePropsFilter.Wildness: wild = new FloatRange(0.25f, 0.75f); return;
				case RacePropsFilter.Petness: petness = new FloatRange(0.25f, 0.75f); return;
				case RacePropsFilter.Trainability: trainability = TrainabilityDefOf.Advanced; return;
			}
		}
	

		public override bool DrawSpecial(Rect rect, WidgetRow row)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			switch (sel)
			{
				case RacePropsFilter.Intelligence:
					if (row.ButtonText(intelligence.TranslateEnum()))
					{
						foreach (Intelligence intel in Enum.GetValues(typeof(Intelligence)))
						{
							options.Add(new FloatMenuOption(intel.TranslateEnum(), () => intelligence = intel));
						}
						MainTabWindow_List.DoFloatMenu(options);
					}
					break;

				case RacePropsFilter.Wildness:
				case RacePropsFilter.Petness:
					ref FloatRange oldRange = ref wild;
					if (sel == RacePropsFilter.Petness)
						oldRange = ref petness;

					FloatRange newRange = oldRange;
					Widgets.FloatRange(rect, id, ref newRange, valueStyle:ToStringStyle.PercentZero);
					if (newRange != oldRange)
					{
						oldRange = newRange;
						return true;
					}
					break;

				case RacePropsFilter.Trainability:
					if (row.ButtonText(trainability.LabelCap))
					{
						foreach (TrainabilityDef def in DefDatabase<TrainabilityDef>.AllDefsListForReading)
						{
							options.Add(new FloatMenuOption(def.LabelCap, () => trainability = def));
						}
						MainTabWindow_List.DoFloatMenu(options);
					}
					break;
			}
			return false;
		}
	}
}