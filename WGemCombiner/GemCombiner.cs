﻿namespace WGemCombiner
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Drawing;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Windows.Forms;
	using Properties;
	using static Globals;
	using static Instruction;
	using static NativeMethods;

	public partial class GemCombiner : Form
	{
		#region Constants
		private const int RidiculousInstructionCount = 200000;
		#endregion

		#region Static Fields
		private static Dictionary<GemColors, string> gemEffectNames = new Dictionary<GemColors, string>()
		{
			[GemColors.Black] = "Bloodbound",
			[GemColors.Kill] = "Kill",
			[GemColors.Mana] = "Mana",
			[GemColors.Orange] = "Leech",
			[GemColors.Red] = "Chain Hit",
			[GemColors.Yellow] = "Critical Hit"
		};
		#endregion

		#region Fields
		private HelpForm helpForm = new HelpForm();
		private Options optionsForm = new Options();
		private bool asyncWaiting = false;
		private Dictionary<string, RecipeCollection> recipes = new Dictionary<string, RecipeCollection>();
		private Stopwatch stopwatch = new Stopwatch();
		#endregion

		#region Constructors
		public GemCombiner()
		{
			foreach (var file in new string[] { "bbound", "kgcomb", "kgcomb-bbound", "kgcomb-exact", "kgspec-appr", "kgspec-exact", "kgspec-kgssemi", "kgspec-mgsappr", "kgspec-mgsexact", "leech", "mgcomb", "mgcomb-exact", "mgcomb-leech", "mgspec-appr", "mgspec-exact" })
			{
				this.AddResourceRecipe(file);
			}

			var combos = new string[] { "mgspec-appr" };
			for (int counter = 0; counter < combos.Length; counter++)
			{
				this.AddResourceCombo(combos[counter], counter + 1);
			}

			this.AddTextFileRecipes(ExePath + @"\recipes.txt");
			this.InitializeComponent();
			this.SettingsHandler_BordersChanged(null, null);
			if ((Skin)Settings.Default.Skin == Skin.Hellrages)
			{
				this.SettingsHandler_SkinChanged(null, null);
			}

#if !DEBUG
			this.testAllButton.Visible = false;
#endif

			CombinePerformer.StepComplete += this.CombinePerformer_StepComplete;
			SettingsHandler.SkinChanged += this.SettingsHandler_SkinChanged;
			SettingsHandler.BordersChanged += this.SettingsHandler_BordersChanged;
			this.TopMost = Settings.Default.TopMost;

			var cb = this.colorComboBox.Items;
			foreach (var key in this.recipes.Keys)
			{
				cb.Add(key);
			}

			this.colorComboBox.SelectedIndex = 0;
			CombinePerformer.Enabled = true;
		}
		#endregion

		#region Form/Control Methods
		private void ColorComboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			var cb = this.combineComboBox.Items;
			cb.Clear();
			foreach (var item in this.recipes[this.colorComboBox.Text])
			{
				cb.Add(item.Gem.Title);
			}

			this.combineComboBox.SelectedIndex = 0; // Preselect the first in the box
		}

		private void CombineButton_Click(object sender, EventArgs e)
		{
			if (this.asyncWaiting)
			{
				return; // there was already a thread waiting for hotkey
			}

			while (GetAsyncKeyState((Keys)Settings.Default.Hotkey) != 0)
			{
				// MessageBox.Show("Key detection failed, or you were already holding hotkey. Try again.");
				Thread.Sleep(500);
			}

			this.combineButton.Text = "Press " + SettingsHandler.HotkeyText + " on A1"; // hotkey
			this.asyncWaiting = true;
			do
			{
				Application.DoEvents();
				Thread.Sleep(10);

				// [HR] Cancel before starting or if form is closing
				if (GetAsyncKeyState(Keys.Escape) != 0 || !CombinePerformer.Enabled)
				{
					this.combineButton.Text = "Combine";
					this.asyncWaiting = false;
					return;
				}
			}
			while (GetAsyncKeyState((Keys)Settings.Default.Hotkey) == 0);

			// User pressed hotkey
			this.asyncWaiting = false;
			CombinePerformer.SleepTime = (int)this.delayNumeric.Value;
			this.stopwatch.Reset();
			this.stopwatch.Start();
			this.combineProgressBar.Maximum = CombinePerformer.Instructions.Count;
			CombinePerformer.PerformCombine((int)this.stepNumeric.Value);

			// Combine finished
			this.combineProgressBar.Value = this.combineProgressBar.Minimum;
			this.GuessEta();
			this.combineButton.Text = "Combine";
			if (Settings.Default.AutoCombine)
			{
				Thread.Sleep(500); // guess give it 0.5sec before going again
				this.combineButton.PerformClick(); // guess it's finished, click the "combine" again
			}
		}

		private void CombineComboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			var combine = this.recipes[this.colorComboBox.Text][this.combineComboBox.Text];
			this.CreateInstructions(combine);
			this.recipeInputRichTextBox.Text = combine.Gem.Recipe();
			if (Settings.Default.AutoCombine)
			{
				this.combineButton.PerformClick(); // Auto-load the combine button so all u have to press is "9" over the gem
			}
		}

		private void DelayNumeric_ValueChanged(object sender, EventArgs e) => this.GuessEta();

		private void ExitButton_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void GemCombiner_FormClosing(object sender, FormClosingEventArgs e)
		{
			CombinePerformer.Enabled = false;
			Settings.Default.Save();
			CombinePerformer.StepComplete -= this.CombinePerformer_StepComplete;
			SettingsHandler.BordersChanged -= this.SettingsHandler_BordersChanged;
			SettingsHandler.SkinChanged -= this.SettingsHandler_SkinChanged;
		}

		private void GemCombiner_MouseDown(object sender, MouseEventArgs e)
		{
			// This part allows you to drag the window around while holding it anywhere
			if (e.Button == MouseButtons.Left)
			{
				ReleaseCapture();
				SendMessage(this.Handle, WmNclButtonDown, HtCaption, IntPtr.Zero);
			}
		}

		private void HelpButton_Click(object sender, EventArgs e)
		{
			this.helpForm.Show();
		}

		private void OptionsButton_Click(object sender, EventArgs e)
		{
			// Open modally or we can trigger the combine while setting the hotkey. Could be worked around in other ways, but it's unlikely that a user will want to leave the Options screen open for any reason.
			this.optionsForm.ShowDialog(this);
		}

		private void ParseRecipeParButton_Click(object sender, EventArgs e)
		{
			this.recipeInputRichTextBox.Text = this.ParseRecipe(false);
		}

		private void ParseRecipeEqsButton_Click(object sender, EventArgs e)
		{
			this.recipeInputRichTextBox.Text = this.ParseRecipe(true);
		}

		private void SlotLimitUpDown_ValueChanged(object sender, EventArgs e)
		{
			Combiner.SlotLimit = (int)this.slotLimitUpDown.Value;
		}

		private void StepNumeric_ValueChanged(object sender, EventArgs e)
		{
			var style = this.stepNumeric.Value == 1 ? FontStyle.Regular : FontStyle.Bold;
			this.stepNumeric.Font = new Font(this.stepNumeric.Font, style);
			this.stepLabel.Font = new Font(this.stepNumeric.Font, style);
			this.GuessEta();
		}
		#endregion

		#region Private Methods
		// Separate functions with a lot of duplicate code for combos until we're sure how exactly we're handling them, then we can optimize it later if appropriate.
		private void AddResourceCombo(string name, int counter)
		{
			var resourceName = "WGemCombiner.Resources.recipes." + name + ".txt";

			using (Stream stream = Assembly.GetManifestResourceStream(resourceName))
			using (StreamReader reader = new StreamReader(stream))
			{
				var file = reader.ReadToEnd().Replace("\r\n", "\n");
				var fileRecipes = file.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < fileRecipes.Length; i += 2)
				{
					var gemRecipe = fileRecipes[i];
					var ampRecipe = fileRecipes[i + 1];
					this.AddCombo(new Combiner(gemRecipe.Split('\n')), new Combiner(ampRecipe.Split('\n')));
				}
			}
		}

		private void AddResourceRecipe(string name)
		{
			var resourceName = "WGemCombiner.Resources.recipes." + name + ".txt";

			using (Stream stream = Assembly.GetManifestResourceStream(resourceName))
			using (StreamReader reader = new StreamReader(stream))
			{
				var file = reader.ReadToEnd().Replace("\r\n", "\n");
				var fileRecipes = file.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
				foreach (var recipe in fileRecipes)
				{
					this.AddRecipe(new Combiner(recipe.Split('\n')));
				}
			}
		}

		private void AddTextFileRecipes(string filename)
		{
			if (File.Exists(filename))
			{
				var lines = File.ReadAllLines(filename);
				var recipe = new List<string>();
				foreach (var line in lines)
				{
					if (!line.StartsWith("#", StringComparison.Ordinal) && !line.StartsWith("//", StringComparison.Ordinal))
					{
						var trimmedLine = line.Trim();
						if (trimmedLine.Length == 0)
						{
							if (recipe.Count > 0)
							{
								this.AddRecipe(new Combiner(recipe));
								recipe.Clear();
							}
						}
						else if (line.Contains("="))
						{
							recipe.Add(line);
						}
						else
						{
							try
							{
								var equations = Combiner.EquationsFromParentheses(trimmedLine);
								var newCombiner = new Combiner(equations);
								this.AddRecipe(newCombiner);
#if DEBUG
								Debug.WriteLine("{3}# {0} {1}, Cost={2}", newCombiner.Gem.Color, newCombiner.Gem.SpecWord, newCombiner.Gem.Cost, Environment.NewLine);
								foreach (var equation in equations)
								{
									Debug.WriteLine(equation.Substring(equation.IndexOf('=') + 1));
								}
#endif
							}
							catch (ArgumentException ex)
							{
								MessageBox.Show(ex.Message, "Error in " + filename, MessageBoxButtons.OK, MessageBoxIcon.Error);
								return;
							}
						}
					}
				}

				if (recipe.Count > 0)
				{
					this.AddRecipe(new Combiner(recipe));
					recipe.Clear();
				}
			}
		}

		private void AddCombo(Combiner gemCombine, Combiner ampCombine)
		{
			var gemGroup = string.Format(CultureInfo.CurrentCulture, "Combo ({0}/{1})", gemCombine.Gem.Cost, ampCombine.Gem.Cost);
			var gem = gemCombine.Gem;
			string gemTitle;
			if (Settings.Default.UseColors)
			{
				gemTitle = gem.Color.ToString();
			}
			else if (!gemEffectNames.TryGetValue(gem.Color, out gemTitle))
			{
				gemTitle = "Other";
			}

			gem.Title = gemTitle + " " + gemCombine.Gem.SpecWord;

			var amp = ampCombine.Gem;
			string ampTitle;
			if (Settings.Default.UseColors)
			{
				ampTitle = amp.Color.ToString();
			}
			else if (!gemEffectNames.TryGetValue(amp.Color, out ampTitle))
			{
				ampTitle = "Other";
			}

			amp.Title = ampTitle + " " + ampCombine.Gem.SpecWord;
			if (!this.recipes.ContainsKey(gemGroup))
			{
				this.recipes[gemGroup] = new RecipeCollection();
			}

			if (!this.recipes[gemGroup].Contains(gem.Title))
			{
				this.recipes[gemGroup].Add(gemCombine);
				this.recipes[gemGroup].Add(ampCombine);
			}
		}

		private void AddRecipe(Combiner combine)
		{
			var gem = combine.Gem;
			gem.Title = string.Format(CultureInfo.CurrentCulture, "{0:0000000} ({1:0.000000}){2}", gem.Cost, gem.Growth, IsPowerOfTwo(gem.Cost) ? "-" : string.Empty);
			string gemGroup;
			if (Settings.Default.UseColors)
			{
				gemGroup = gem.Color.ToString();
			}
			else if (!gemEffectNames.TryGetValue(gem.Color, out gemGroup))
			{
				gemGroup = "Other";
			}

			gemGroup += " " + combine.Gem.SpecWord;
			if (!this.recipes.ContainsKey(gemGroup))
			{
				this.recipes[gemGroup] = new RecipeCollection();
			}

			if (!this.recipes[gemGroup].Contains(gem.Title))
			{
				// TODO: Consider some other method of checking if these truly are duplicates or not.
				// Ignores gems with identical CombineTitles. Conceivably, there could be two different combines with identical titles, but I think this is fairly unlikely.
				this.recipes[gemGroup].Add(combine);
			}
		}

		private void CombinePerformer_StepComplete(object sender, int step)
		{
			Application.DoEvents();
			if (GetAsyncKeyState(Keys.Escape) != 0)
			{
				CombinePerformer.CancelCombine = true;
			}

			this.combineButton.Text = step.ToString(CultureInfo.CurrentCulture);
			this.combineProgressBar.Value = step;
			this.GetRealEta(step);
		}

		private void CreateInstructions(Combiner combine)
		{
			try
			{
				var instructions = combine.CreateInstructions();
				if (instructions.Count > RidiculousInstructionCount)
				{
					throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Creating this gem in {0} slots would require an excessive number of steps ({1}).", Combiner.SlotLimit, instructions.Count));
				}

				this.resultLabel.Text = combine.Gem.DisplayInfo + string.Format(CultureInfo.CurrentCulture, "\r\nSlots:  {0}\r\nSteps:  {1}", instructions.SlotsRequired, instructions.Count);
				this.baseGemsListBox.Items.Clear();

				var baseGems = new List<BaseGem>(combine.BaseGems);
				baseGems.Sort((g1, g2) => g1.OriginalSlot.CompareTo(g2.OriginalSlot));
				foreach (var gem in baseGems)
				{
					if (gem.OriginalSlot != Combiner.NotSlotted)
					{
						this.baseGemsListBox.Items.Add(SlotName(gem.OriginalSlot) + ": " + gem.Color.ToString());
					}
				}

				var sb = new StringBuilder();
				for (int i = 1; i <= instructions.Count; i++)
				{
					sb.AppendLine(i.ToString(CultureInfo.CurrentCulture) + ": " + instructions[i - 1].ToString());
				}

				this.instructionsTextBox.Text = sb.ToString();
				this.instructionsTextBox.SelectionLength = 0;
				this.instructionsTextBox.SelectionStart = 0;
				this.instructionsTextBox.ScrollToCaret();
				this.stepNumeric.Minimum = instructions.Count == 0 ? 0 : 1;
				this.stepNumeric.Maximum = instructions.Count;

				CombinePerformer.Instructions = instructions;
				this.GuessEta();
			}
			catch (InvalidOperationException ex)
			{
				MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void FormatEta(TimeSpan eta)
		{
			string separator = CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator;
			string format = "h\\" + separator + "mm\\" + separator + "ss";
			this.combineProgressBar.Text = "ETA: " + eta.ToString(format, CultureInfo.CurrentCulture);
		}

		private void GetRealEta(int step)
		{
			var time = this.stopwatch.ElapsedMilliseconds;
			var eta = ((time * CombinePerformer.Instructions.Count) / step) - time;
			this.FormatEta(new TimeSpan(0, 0, 0, 0, (int)eta));
		}

		private void GuessEta()
		{
			// Overhead beyond the delay time is usually around 2.5-3ms, so be safe and use 3.
			double eta = CombinePerformer.Instructions == null ? 0 : ((double)this.delayNumeric.Value + 3) * (CombinePerformer.Instructions.Count - ((int)this.stepNumeric.Value - 1));
			this.FormatEta(new TimeSpan(0, 0, 0, 0, (int)eta));
		}

		private string ParseRecipe(bool asEquations)
		{
			var lines = this.recipeInputRichTextBox.Lines;
			var newLines = new List<string>();
			bool equations = false;
			foreach (var line in lines)
			{
				if (!line.StartsWith("#", StringComparison.CurrentCulture) && !line.StartsWith("//", StringComparison.CurrentCulture))
				{
					newLines.Add(line);
					equations |= line.Contains("=");
				}
			}

			Combiner combine;
			try
			{
				if (equations)
				{
					combine = new Combiner(newLines);
				}
				else
				{
					newLines = new List<string>(Combiner.EquationsFromParentheses(string.Join(string.Empty, newLines))); // Join in case someone uses line breaks for formatting
					combine = new Combiner(newLines);
				}
			}
			catch (ArgumentException ex)
			{
				MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return null;
			}

			if (combine != null && combine.Gem != null)
			{
				this.CreateInstructions(combine);
			}

			return asEquations ? string.Join(Environment.NewLine, newLines) : combine.Gem.Recipe();
		}

		private void SettingsHandler_BordersChanged(object sender, EventArgs e) => SettingsHandler.ApplyBorders(this);

		private void SettingsHandler_SkinChanged(object sender, EventArgs e)
		{
			SettingsHandler.ChangeFormSize(this);
			SettingsHandler.ApplySkin(this);
		}

		private void TestAll_Click(object sender, EventArgs e)
		{
			foreach (var kvp in this.recipes)
			{
				foreach (var combine in kvp.Value)
				{
					var instructions = combine.CreateInstructions();
					try
					{
						instructions.Verify(combine.BaseGems);
					}
					catch (InvalidOperationException ex)
					{
						MessageBox.Show(ex.Message, "Verification failed", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					}
				}
			}

			MessageBox.Show("Testing complete!", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
		#endregion
	}
}
