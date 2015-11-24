﻿namespace WGemCombiner
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using static Globals;
	using static Localization;

	#region Public Enums
	public enum GemColor
	{
		Orange,
		Black,
		Mana,
		Yellow,
		Kill,
		Red,
	}
	#endregion

	public class Gem : IComparable<Gem>
	{
		#region Static Fields
		private static SortedDictionary<char, GemColor> gemTypes = new SortedDictionary<char, GemColor>()
		{
			['b'] = GemColor.Black,
			['k'] = GemColor.Kill,
			['m'] = GemColor.Mana,
			['o'] = GemColor.Orange,
			['r'] = GemColor.Red,
			['y'] = GemColor.Yellow,
		};

		private static SortedDictionary<GemColor, string> gemNames = new SortedDictionary<GemColor, string>()
		{
			[GemColor.Black] = "Black",
			[GemColor.Kill] = "Kill gem",
			[GemColor.Mana] = "Mana gem",
			[GemColor.Orange] = "Orange",
			[GemColor.Red] = "Red",
			[GemColor.Yellow] = "Yellow",
		};

		private static StringBuilder combineBuilder = new StringBuilder();
		#endregion

		#region Fields
		// Components
		private double blood;
		private double critMult;
		private double damage; // max damage
		private double leech;
		#endregion

		#region Constructors
		public Gem(char letter)
		{
			var color = gemTypes[letter];
			this.Letter = letter;
			this.Color = color;
			this.GradeGrowth = 0;
			this.Cost = 1;
			this.damage = 0; // gems that need damage will have that properly setted (dmg_yellow=1)

			if (color == GemColor.Black)
			{
				this.damage = 1.186168;
				this.blood = 1.0;
			}
			else if (color == GemColor.Kill)
			{
				this.damage = 1.0;
				this.critMult = 1.0;
				this.blood = 1.0;
			}
			else if (color == GemColor.Mana)
			{
				this.leech = 1.0;
				this.blood = 1.0;
			}
			else if (color == GemColor.Orange)
			{
				this.leech = 1.0;
			}
			else if (color == GemColor.Yellow)
			{
				this.damage = 1.0;
				this.critMult = 1.0;
			}
			else if (color == GemColor.Red)
			{
				this.damage = 0.909091;
			}
		}

		public Gem(Gem gem1, Gem gem2)
		{
			ThrowNull(gem1, nameof(gem1));
			ThrowNull(gem2, nameof(gem2));
			gem1.UseCount++;
			gem2.UseCount++;
			if (gem2.Cost > gem1.Cost)
			{
				this.Component1 = gem2;
				this.Component2 = gem1;
			}
			else
			{
				this.Component1 = gem1;
				this.Component2 = gem2;
			}

			if (gem1.Color == gem2.Color)
			{
				this.Color = gem1.Color;
			}
			else
			{
				if (gem1.Color == GemColor.Kill || gem2.Color == GemColor.Kill || gem1.Color == GemColor.Yellow || gem2.Color == GemColor.Yellow)
				{
					// Since the colors aren't the same and yellow is involved, this must be a kill gem.
					this.Color = GemColor.Kill;
				}
				else if (gem1.Color == GemColor.Mana || gem2.Color == GemColor.Mana || gem1.Color == GemColor.Orange || gem2.Color == GemColor.Orange)
				{
					// Since the colors aren't the same and orange is involved, this must be a mana gem.
					this.Color = GemColor.Mana;
				}
				else
				{
					this.Color = GemColor.Red; // Ignore any black component for now and call this red. Since it's not yellow or orange, it'll still be picked up as part of a kill/mana gem in subsequent combines.
				}
			}

			this.GradeGrowth = (gem1.GradeGrowth > gem2.GradeGrowth) ? gem1.GradeGrowth : gem2.GradeGrowth;
			if (gem1.GradeGrowth == gem2.GradeGrowth)
			{
				this.GradeGrowth++;
				this.damage = CombineCalc(gem1.damage, gem2.damage, 0.87, 0.71);
				this.leech = CombineCalc(gem1.leech, gem2.leech, 0.88, 0.50);
				this.blood = CombineCalc(gem1.blood, gem2.blood, 0.78, 0.31);
				this.critMult = CombineCalc(gem1.critMult, gem2.critMult, 0.88, 0.50);
			}
			else if (gem1.GradeGrowth == gem2.GradeGrowth + 1)
			{
				this.damage = CombineCalc(gem1.damage, gem2.damage, 0.86, 0.70);
				this.leech = CombineCalc(gem1.leech, gem2.leech, 0.89, 0.44);
				this.blood = CombineCalc(gem1.blood, gem2.blood, 0.79, 0.29);
				this.critMult = CombineCalc(gem1.critMult, gem2.critMult, 0.88, 0.44);
			}
			else if (gem1.GradeGrowth == gem2.GradeGrowth - 1)
			{
				this.damage = CombineCalc(gem1.damage, gem2.damage, 0.86, 0.70);
				this.leech = CombineCalc(gem1.leech, gem2.leech, 0.89, 0.44);
				this.blood = CombineCalc(gem1.blood, gem2.blood, 0.79, 0.29);
				this.critMult = CombineCalc(gem1.critMult, gem2.critMult, 0.88, 0.44);
			}
			else
			{
				this.damage = CombineCalc(gem1.damage, gem2.damage, 0.85, 0.69);
				this.leech = CombineCalc(gem1.leech, gem2.leech, 0.90, 0.38);
				this.blood = CombineCalc(gem1.blood, gem2.blood, 0.80, 0.27);
				this.critMult = CombineCalc(gem1.critMult, gem2.critMult, 0.88, 0.44);
			}

			this.damage = Math.Max(this.damage, Math.Max(gem1.damage, gem2.damage));
			this.Cost = gem1.Cost + gem2.Cost;
			this.Growth = Math.Log(this.Power, this.Cost);
		}

		private Gem()
		{
		}
		#endregion

		#region Public Properties
		public GemColor Color { get; }

		public string ColorName => gemNames[this.Color];

		public string CombineTitle => CurrentCulture($"{this.Cost:000000} ({this.Growth:0.00000}){(IsPowerOfTwo(this.Cost) ? "-" : string.Empty)}");

		public Gem Component1 { get; set; }

		public Gem Component2 { get; set; }

		public int Cost { get; set; }

		public int GradeGrowth { get; set; }

		public double Growth { get; set; } = 1; // ???? Math.Log10(1.379) / Math.Log10(2);

		public int ID { get; set; }

		public bool IsBaseGem => this.GradeGrowth == 0;

		public char Letter { get; }

		public double Power
		{
			get
			{
				switch (this.Color)
				{
					case GemColor.Orange:
						return this.leech;
					case GemColor.Black:
						return this.blood;
					case GemColor.Mana:
						return this.leech * this.blood;
					case GemColor.Yellow:
						return this.damage * this.critMult;
					case GemColor.Kill:
						return this.damage * this.critMult * this.blood * this.blood; // 1+blood makes no sense, g1 bb is set to 1 by convention
					default:
						return 0; // Red have no mana or kill power
				}
			}
		}

		public int Slot { get; set; }

		public int UseCount { get; set; }
		#endregion

		#region Public Methods
		public int CompareTo(Gem other) => this.Cost.CompareTo(other?.Cost);

		public string DisplayInfo(bool showAll, int slots)
		{
			var retval = CurrentCulture($"Grade: +{this.GradeGrowth}\r\nCost: {this.Cost}x\r\nGrowth: {this.Growth:0.0####}\r\nSlots: {slots}");
			if (showAll)
			{
				retval += CurrentCulture($"\r\nPower: {this.Power:0.0####}\r\nDamage: {this.damage:0.0####}\r\nLeech: {this.leech:0.0####}\r\nCrit: {this.critMult:0.0####}\r\nBbound: {this.blood:0.0####}");
			}

			return retval;
		}

		public string GetFullCombine()
		{
			combineBuilder.Clear();
			this.DoFullCombine();
			var retval = combineBuilder.ToString();
			combineBuilder.Clear();
			return retval;
		}
		#endregion

		#region Public Override Methods
		public override string ToString() => this.CombineTitle;
		#endregion

		#region Private Static Methods
		private static double CombineCalc(double value1, double value2, double multHigh, double multLow) => value1 > value2 ? (multHigh * value1) + (multLow * value2) : (multHigh * value2) + (multLow * value1);

		private static bool IsPowerOfTwo(int cost) => (cost != 0) && (cost & (cost - 1)) == 0;
		#endregion

		#region Private Methods
		private void DoFullCombine()
		{
			this.Component1.DoSubCombine();
			combineBuilder.Append("+");
			this.Component2.DoSubCombine();
		}

		private void DoSubCombine()
		{
			if (this.IsBaseGem)
			{
				combineBuilder.Append(this.Letter);
			}
			else
			{
				combineBuilder.Append("(");
				this.DoFullCombine();
				combineBuilder.Append(")");
			}
		}

		// private string GetSubCombine() => this.IsBaseGem ? this.ID : "(" + this.GetFullCombine() + ")";
		#endregion
	}
}