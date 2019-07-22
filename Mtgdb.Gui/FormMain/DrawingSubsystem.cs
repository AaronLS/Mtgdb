﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mtgdb.Controls;
using Mtgdb.Data;
using Mtgdb.Data.Index;
using Mtgdb.Ui;

namespace Mtgdb.Gui
{
	[Localizable(false)]
	public class DrawingSubsystem
	{
		public DrawingSubsystem(
			LayoutViewControl viewCards,
			LayoutViewControl viewDeck,
			CardSearchSubsystem cardSearchSubsystem,
			CardDocumentAdapter adapter,
			DeckEditorModel deckEditorModel,
			CountInputSubsystem countInputSubsystem,
			QuickFilterFacade quickFilterFacade,
			LegalitySubsystem legalitySubsystem,
			IconRecognizer iconRecognizer)
		{
			_viewCards = viewCards;
			_viewDeck = viewDeck;
			_deckEditorModel = deckEditorModel;
			_countInputSubsystem = countInputSubsystem;
			_quickFilterFacade = quickFilterFacade;
			_legalitySubsystem = legalitySubsystem;

			_viewCards.RowDataLoaded += setHighlightMatches;
			_viewCards.IconRecognizer = iconRecognizer;

			_highlightSubsystem = new SearchResultHighlighter(
				cardSearchSubsystem,
				adapter,
				new KeywordHighlighter());
		}

		public void SetupDrawingCardEvent()
		{
			_viewCards.CustomDrawField += drawCard;
			_viewDeck.CustomDrawField += drawCard;
		}

		private void drawCard(object sender, CustomDrawArgs e)
		{
			var view = (LayoutViewControl) sender;
			var card = (Card)view.FindRow(e.RowHandle);

			if (card == null)
				return;

			if (e.FieldName != nameof(Card.Image))
				return;

			e.Handled = true;

			var image = card.Image(Ui);

			if (image != null)
			{
				var bounds = image.Size.FitIn(e.Bounds);
				e.Graphics.DrawImage(image, bounds);
			}

			if (card == _deckEditorModel.TouchedCard)
				drawSelection(e, SystemColors.ActiveCaption, SystemColors.GradientActiveCaption, 255);
			else
			{
				int deckCount = card.DeckCount(Ui);
				int collectionCount = card.CollectionCount(Ui);

				var colorSelection = SystemColors.InactiveCaption;
				var colorSelectionGradient = SystemColors.GradientInactiveCaption;

				if (deckCount > 0)
					drawSelection(e, colorSelection, colorSelectionGradient, 127 + 32);
				else if (collectionCount > 0 || e.HotTracked)
					drawSelection(e, colorSelection, colorSelectionGradient, 127 - 32);
			}

			drawLegalityWarning(e, sender, card);
			drawCountWarning(e, card);
			drawCount(sender, e, card);
		}

		private void drawLegalityWarning(CustomDrawArgs e, object sender, Card card)
		{
			var legalityWarning = _legalitySubsystem.GetWarning(card);

			int deckCount = card.DeckCount(Ui);

			if (legalityWarning == Legality.Restricted && deckCount <= 1 && sender == _viewDeck)
				legalityWarning = null;

			if (string.IsNullOrEmpty(legalityWarning))
				return;

			var rect = getLegalityWarningRectangle(e);
			var font = getWarningFont();

			var lineSize = e.Graphics.MeasureText(legalityWarning, font);
			rect.Offset((int) ((rect.Width - lineSize.Width) / 2f), 0);

			e.Graphics.DrawText(legalityWarning, font, rect,
				Color.FromArgb(255 - 16,
					SystemColors.Highlight.TransformHsv(
						h: _ => _ + Color.DodgerBlue.RotationTo(Color.OrangeRed)))
			);
		}

		private Font getWarningFont()
		{
			var countFont = _countInputSubsystem.CountFont;
			var font = new Font(
				FontFamily.GenericMonospace,
				countFont.Height - _countInputSubsystem.CountBorder,
				FontStyle.Italic | FontStyle.Bold,
				countFont.Unit);
			return font;
		}

		private void drawCountWarning(CustomDrawArgs e, Card card)
		{
			int countInMain = _deckEditorModel.MainDeck.GetCount(card.Id);
			int countInSideboard = _deckEditorModel.SideDeck.GetCount(card.Id);

			if (countInMain == 0 || countInSideboard == 0)
				// the excessive count is not due to main + side sum
				// therefore it is obvious, warning is not necessary
				return;

			var totalCount = countInMain + countInSideboard;

			Color color;
			int maxCount;

			if (totalCount > card.MaxCountInDeck())
			{
				maxCount = card.MaxCountInDeck();
				color = SystemColors.HotTrack.TransformHsv(
					h: _ => _ + Color.Blue.RotationTo(Color.Crimson));
			}
			else
			{
				int collectionCount = card.CollectionCount(Ui);

				if (totalCount > collectionCount && collectionCount > 0)
				{
					maxCount = collectionCount;
					color = SystemColors.HotTrack;
				}
				else
					return;
			}

			string warning;
			if (countInMain == 0)
				warning = $"{countInSideboard}/{maxCount}";
			else if (countInSideboard == 0)
				warning = $"{countInMain}/{maxCount}";
			else
				warning = $"{countInMain}+{countInSideboard}/{maxCount}";

			var rect = getCountWarningRectangle(e);
			var font = getWarningFont();

			var lineSize = e.Graphics.MeasureText(warning, font);
			rect.Offset((int) ((rect.Width - lineSize.Width) / 2f), 0);

			e.Graphics.DrawText(warning, font, rect, Color.FromArgb(224, color));
		}

		private void drawSelection(CustomDrawArgs e, Color colorGrad1, Color colorGrad2, int opacity)
		{
			var rect = _countInputSubsystem.GetCountRectangle(e.Bounds);

			int countBorder = _countInputSubsystem.CountBorder;
			const int cornerOffset = -1;
			const int cornerShare = 4;

			rect.Inflate(-countBorder, -countBorder);

			int cornerYSize = rect.Height / cornerShare;
			int cornerXSize = rect.Height / cornerShare;

			var points = new[]
			{
				new Point(rect.Left - cornerOffset + cornerXSize, rect.Top - cornerOffset),
				new Point(rect.Right + cornerOffset - cornerXSize, rect.Top - cornerOffset),

				new Point(rect.Right + cornerOffset, rect.Top - cornerOffset + cornerYSize),
				new Point(rect.Right + cornerOffset, rect.Bottom + cornerOffset - cornerYSize),

				new Point(rect.Right + cornerOffset - cornerXSize, rect.Bottom + cornerOffset),
				new Point(rect.Left - cornerOffset + cornerXSize, rect.Bottom + cornerOffset),

				new Point(rect.Left - cornerOffset, rect.Bottom + cornerOffset - cornerYSize),
				new Point(rect.Left - cornerOffset, rect.Top - cornerOffset + cornerYSize)
			};

			var gradientRect = rect;
			gradientRect.Inflate((int) (-rect.Width * 0.33f), (int) (-rect.Height * 0.16f));

			var brush = new LinearGradientBrush(
				gradientRect,
				Color.FromArgb(opacity, colorGrad1),
				Color.FromArgb(opacity, colorGrad2),
				LinearGradientMode.BackwardDiagonal);

			brush.SetBlendTriangularShape(0.99f, 1f);
			e.Graphics.FillClosedCurve(brush, points, FillMode.Alternate, 0.05f);
		}

		private void drawCount(object sender, CustomDrawArgs e, Card card)
		{
			var countText = getCountText(sender, card);
			if (string.IsNullOrEmpty(countText))
				return;

			var rect = _countInputSubsystem.GetCountRectangle(e.Bounds);
			var font = _countInputSubsystem.CountFont;

			var textSize = e.Graphics.MeasureText(countText, font);

			var targetRect = new Rectangle(
				(int) Math.Ceiling(rect.Left + 0.5f * (rect.Width - textSize.Width)),
				(int) Math.Ceiling(rect.Top + 0.5f * (rect.Height - textSize.Height)),
				textSize.Width,
				textSize.Height);

			targetRect.Offset(0, 1);
			e.Graphics.DrawText(countText, font, targetRect, SystemColors.WindowText);
		}

		private Rectangle getLegalityWarningRectangle(CustomDrawArgs e)
		{
			var stripSize = new Size(Ui.ImageLoader.CardSize.Width,
				getWarningFont().Height + _countInputSubsystem.CountBorder);

			var rect = new Rectangle(
				e.Bounds.Left,
				(int) (e.Bounds.Bottom - 2.625f * stripSize.Height),
				stripSize.Width,
				stripSize.Height);

			return rect;
		}

		private Rectangle getCountWarningRectangle(CustomDrawArgs e)
		{
			var result = getLegalityWarningRectangle(e);
			result.Offset(new Point(0, -result.Height));
			return result;
		}

		private string getCountText(object sender, Card card)
		{
			var countText = new StringBuilder();

			int deckCount = card.DeckCount(Ui);

			if (deckCount > 0)
				countText.Append(deckCount);

			int collectionCount = card.CollectionCount(Ui);
			if (collectionCount > 0 && (
				sender == _viewDeck && _deckEditorModel.CurrentZone != Zone.SampleHand ||
				sender == _viewCards))
			{
				countText.AppendFormat(@" / {0}", collectionCount);
			}

			string countTextStr = countText.ToString();
			return countTextStr;
		}

		private void setHighlightMatches(object sender, int rowHandle)
		{
			var view = (LayoutViewControl)sender;
			var card = (Card) view.FindRow(rowHandle);
			if (card == null)
				return;

			foreach (var displayField in view.FieldNames)
			{
				if (Str.Equals(displayField, nameof(Card.Image)))
					continue;

				var displayText = _viewCards.GetText(rowHandle, displayField);
				var matches = new List<TextRange>();
				var contextMatches = new List<TextRange>();

				addFilterButtonMatches(matches, displayField, displayText);
				_highlightSubsystem.AddSearchStringMatches(matches, contextMatches, displayField, displayText);
				addLegalityMatches(matches, displayField, displayText);

				var highlightRanges = _highlightSubsystem.GetHighlightRanges(matches, contextMatches);
				_viewCards.SetHighlightTextRanges(highlightRanges, rowHandle, displayField);
			}
		}

		private void addFilterButtonMatches(List<TextRange> matches, string fieldName, string text)
		{
			var quickFilterMatches = _quickFilterFacade.GetMatches(text, fieldName);

			if (quickFilterMatches == null)
				return;

			matches.AddRange(quickFilterMatches);
		}

		private void addLegalityMatches(List<TextRange> matches, string fieldName, string text)
		{
			if (string.IsNullOrEmpty(_legalitySubsystem.FilterFormat))
				return;

			if (fieldName != nameof(Card.Rulings))
				return;

			var legalityMatches = RegexUtil.GetCached(_legalitySubsystem.FilterFormat).Matches(text)
				.OfType<Match>()
				.Where(_ => _.Success)
				.Select(TextRange.Copy);

			matches.AddRange(legalityMatches);
		}

		public UiModel Ui { get; set; }


		private readonly LayoutViewControl _viewCards;
		private readonly LayoutViewControl _viewDeck;

		private readonly DeckEditorModel _deckEditorModel;
		private readonly CountInputSubsystem _countInputSubsystem;
		private readonly QuickFilterFacade _quickFilterFacade;
		private readonly LegalitySubsystem _legalitySubsystem;
		private readonly SearchResultHighlighter _highlightSubsystem;
	}
}