﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Mtgdb.Controls;
using Mtgdb.Dal;
using Mtgdb.Gui.Properties;

namespace Mtgdb.Gui
{
	public class DeckEditorUi
	{
		private readonly MtgLayoutView _layoutViewCards;
		private readonly MtgLayoutView _layoutViewDeck;
		private readonly Cursor _cursor;
		private readonly DeckEditorModel _deckEditorModel;
		private readonly CollectionEditorModel _collectionModel;
		private readonly DraggingSubsystem _draggingSubsystem;
		private readonly FormZoom _formZoom;
		private readonly Cursor _zoomCursor;
		private readonly Cursor _textSelectionCursor;

		public DeckEditorUi(
			MtgLayoutView layoutViewCards,
			MtgLayoutView layoutViewDeck,
			DeckEditorModel deckEditorModel,
			CollectionEditorModel collectionModel,
			DraggingSubsystem draggingSubsystem,
			Cursor cursor,
			FormZoom formZoom)
		{
			_layoutViewCards = layoutViewCards;
			_layoutViewDeck = layoutViewDeck;
			_cursor = cursor;
			_deckEditorModel = deckEditorModel;
			_collectionModel = collectionModel;
			_draggingSubsystem = draggingSubsystem;
			_draggingSubsystem.DraggedLikeClick += draggedLikeClick;
			_draggingSubsystem.DragRemoved += dragRemoved;
			_draggingSubsystem.DragAdded += dragAdded;
			_layoutViewCards.SelectionStarted += selectionStarted;

			_formZoom = formZoom;

			var hotSpot = Size.Empty.ByDpi();
			_zoomCursor = CursorHelper.CreateCursor(Resources.zoom_48.HalfResizeDpi(), hotSpot);

			var iBeamIcon = Resources.text_selection_24.ResizeDpi();
			var iBeamHotSpot = new Size(iBeamIcon.Width / 2, iBeamIcon.Height / 2);
			_textSelectionCursor = CursorHelper.CreateCursor(iBeamIcon, iBeamHotSpot);
		}

		private void dragRemoved(Card card, Zone fromDeckZone)
		{
			int count = Control.ModifierKeys == Keys.Control ? 4 : 1;
			_deckEditorModel.Add(card, -count, fromDeckZone);
		}

		private void dragAdded(Card card, Zone? fromDeckZone)
		{
			int count = Control.ModifierKeys == Keys.Control ? 4 : 1;

			if (fromDeckZone.HasValue && _deckEditorModel.Zone != Zone.SampleHand)
				_deckEditorModel.Add(card, -count, zone: fromDeckZone, changeTerminatesBatch: false);

			_deckEditorModel.Add(card, +count);
		}

		public void SubscribeToEvents()
		{
			_layoutViewCards.MouseLeave += gridMouseLeave;
			_layoutViewDeck.MouseLeave += gridMouseLeave;

			_layoutViewCards.MouseMove += gridMouseMove;
			_layoutViewDeck.MouseMove += gridMouseMove;

			_layoutViewCards.MouseClicked += gridMouseClick;
			_layoutViewDeck.MouseClicked += gridMouseClick;
		}

		private void gridMouseLeave(object sender, EventArgs e)
		{
			if (_deckEditorModel.IsDragging())
				return;

			updateCursor(getView(sender).Control, outside: true);
		}

		private void gridMouseMove(object sender, MouseEventArgs e)
		{
			var view = getView(sender);

			if (_deckEditorModel.IsDragging())
				return;

			var hitInfo = view.CalcHitInfo(e.Location);
			var card = (Card) view.GetRow(hitInfo.RowHandle);

			if (card != null)
			{
				updateCursor(view.Control,
					overImage: hitInfo.IsOverImage(),
					overText: hitInfo.IsOverText(),
					overButton: hitInfo.IsSomeButton);
			}
			else
			{
				updateCursor(view.Control);
			}
		}

		private void updateCursor(Control control, bool overImage = false, bool overText = false, bool overButton = false, bool outside = false)
		{
			if (outside)
				control.Cursor = _cursor;
			else if (overButton)
				control.Cursor = Cursors.Default;
			else if (overImage)
				control.Cursor = _zoomCursor;
			else if (overText)
				control.Cursor = _textSelectionCursor;
			else
				control.Cursor = Cursors.Default;
		}

		private void gridMouseClick(object sender, HitInfo hitInfo, MouseEventArgs e)
		{
			if (_draggingSubsystem.IsDragging())
				return;

			var view = getView(sender);

			if (hitInfo.AlignButtonDirection.HasValue)
				return;

			var card = getCard(view, hitInfo);

			if (card == null)
				return;

			var (countDelta, isDeck) = getChange(hitInfo, e);

			if (countDelta != 0)
			{
				if (isDeck)
					_deckEditorModel.Add(card, countDelta);
				else
					changeCountInCollection(card, countDelta);

				return;
			}

			if (e.Button == MouseButtons.Left)
				zoomCard(card);
		}

		private static (int CountDelta, bool IsDeck) getChange(HitInfo hitInfo, MouseEventArgs e)
		{
			int countDelta = DeckEditorButtons.GetCountDelta(hitInfo.CustomButtonIndex);

			if (countDelta != 0)
				return (countDelta, DeckEditorButtons.IsDeck(hitInfo.CustomButtonIndex));

			int deltaAbs = (Control.ModifierKeys & Keys.Control) > 0
				? 4
				: 1;

			bool isDeck = (Control.ModifierKeys & Keys.Alt) == 0;

			if (e.Button == MouseButtons.Middle)
				return (-deltaAbs, isDeck);
			
			if (e.Button == MouseButtons.Right)
				return (+deltaAbs, isDeck);

			return (0, true);
		}

		private static Card getCard(MtgLayoutView view, HitInfo hitInfo)
		{
			if (!hitInfo.IsOverImage() && hitInfo.CustomButtonIndex < 0)
				return null;

			var card = (Card) view.GetRow(hitInfo.RowHandle);
			return card;
		}

		private void zoomCard(Card card)
		{
			if (!card.HasImage(Ui))
				return;

			_formZoom.LoadImages(card, Ui);
			_formZoom.ShowImages();
		}

		private void changeCountInCollection(Card card, int increment)
		{
			_collectionModel.Add(card, increment);
		}

		private void draggedLikeClick(Card card)
		{
			// клик с нечаянным микродвижением мышкой был воспринят системой как запуск drag-n-drop
			// для дружелюбия отреагируем, как на нормальный клик - покажем увеличение карты

			if (card != null)
				zoomCard(card);
		}

		private static void selectionStarted(object sender, HitInfo hitInfo, CancelEventArgs cancelArgs)
		{
			if (hitInfo.IsOverImage() || hitInfo.CustomButtonIndex >= 0)
				cancelArgs.Cancel = true;
		}

		private MtgLayoutView getView(object view)
		{
			if (_layoutViewCards.Wraps(view))
				return _layoutViewCards;

			if (_layoutViewDeck.Wraps(view))
				return _layoutViewDeck;

			throw new Exception(@"wrapper not found");
		}



		public UiModel Ui { get; set; }
	}
}