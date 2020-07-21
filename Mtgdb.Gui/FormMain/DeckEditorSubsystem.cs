﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Mtgdb.Controls;
using Mtgdb.Data;
using Mtgdb.Gui.Properties;
using Mtgdb.Ui;

namespace Mtgdb.Gui
{
	public class DeckEditorSubsystem: IComponent
	{
		public DeckEditorSubsystem(
			LayoutViewControl viewCards,
			LayoutViewControl viewDeck,
			DeckEditorModel deckEditorModel,
			CollectionEditorModel collectionModel,
			DraggingSubsystem draggingSubsystem,
			CountInputSubsystem countInputSubsystem,
			Cursor cursor,
			FormZoom formZoom,
			FormMain parent)
		{
			_viewCards = viewCards;
			_viewDeck = viewDeck;
			_cursor = cursor;
			_deckEditorModel = deckEditorModel;
			_collectionModel = collectionModel;
			_draggingSubsystem = draggingSubsystem;
			_countInputSubsystem = countInputSubsystem;

			_formZoom = formZoom;
			_parent = parent;
			_ctsLifetime = new CancellationTokenSource();
			_textSelectionCursor = Cursors.IBeam;
		}

		public void Scale()
		{
			new DpiScaler<DeckEditorSubsystem>(s =>
			{
				var hotSpot = Point.Empty.ByDpi();
				var image = Runtime.IsMono
					? Dpi.ScalePercent > 100
						? Resources.zoom_48_bw.HalfResizeDpi()
						: Resources.zoom_24_bw.ResizeDpi()
					: Resources.zoom_48.HalfResizeDpi();
				s._zoomCursor = CursorHelper.CreateCursor(image, hotSpot);
			}).Setup(this);
		}

		private void dragRemoved(Card card, Zone fromDeckZone)
		{
			int count = Control.ModifierKeys == Keys.Control ? 4 : 1;
			_deckEditorModel.Add(card, -count, zone: fromDeckZone);
		}

		private void dragAdded(Card card, Card at, Zone? fromDeckZone)
		{
			int count = Control.ModifierKeys == Keys.Control ? 4 : 1;

			if (fromDeckZone.HasValue && _deckEditorModel.CurrentZone != Zone.SampleHand)
				_deckEditorModel.Add(card, -count, at, zone: fromDeckZone, changeTerminatesBatch: false);

			_deckEditorModel.Add(card, +count, at);
		}

		public void SubscribeToEvents()
		{
			_viewCards.MouseLeave += gridMouseLeave;
			_viewDeck.MouseLeave += gridMouseLeave;

			_viewCards.MouseMove += gridMouseMove;
			_viewDeck.MouseMove += gridMouseMove;

			_viewCards.MouseClicked += gridMouseClick;
			_viewDeck.MouseClicked += gridMouseClick;

			_draggingSubsystem.DraggedLikeClick += draggedLikeClick;
			_draggingSubsystem.DragRemoved += dragRemoved;
			_draggingSubsystem.DragAdded += dragAdded;
			_viewCards.SelectionStarted += selectionStarted;

			_countInputSubsystem.Input += countInput;
		}

		private void gridMouseLeave(object sender, EventArgs e)
		{
			if (_deckEditorModel.IsDragging)
				return;

			updateCursor((LayoutViewControl)sender, outside: true);
		}

		private void gridMouseMove(object sender, MouseEventArgs e)
		{
			var view = (LayoutViewControl)sender;
			if (_deckEditorModel.IsDragging)
				return;

			var hitInfo = view.CalcHitInfo(e.Location);
			var card = (Card) view.FindRow(hitInfo.RowHandle);

			if (card != null)
				updateCursor(view,
					overImage: hitInfo.IsOverImage(),
					overText:
					hitInfo.IsOverText() ||
					_countInputSubsystem.IsCountRectangle(hitInfo, e.Location, out _),
					overButton: hitInfo.IsSomeButton);
			else
				updateCursor(view);
		}

		private void updateCursor(
			LayoutViewControl control,
			bool overImage = false,
			bool overText = false,
			bool overButton = false,
			bool outside = false)
		{
			if (outside)
				control.Cursor = _cursor;
			else if (overButton)
				control.Cursor = Cursors.Default;
			else if (overText)
				control.Cursor = _textSelectionCursor;
			else if (overImage)
				control.Cursor = _zoomCursor;
			else
				control.Cursor = Cursors.Default;
		}

		private void gridMouseClick(object sender, HitInfo hitInfo, MouseEventArgs e)
		{
			if (_draggingSubsystem.IsDragging)
				return;

			if (hitInfo.AlignButtonDirection.HasValue)
				return;

			var card = _countInputSubsystem.GetCard((LayoutViewControl)sender, hitInfo);
			if (card == null)
				return;

			var viewControl = (LayoutViewControl)sender;
			if (_countInputSubsystem.HandleClick(viewControl, card, e))
				return;

			var (countDelta, isDeck) = getChange(hitInfo, e);
			if (handleCountChange(countDelta, isDeck, card))
				_countInputSubsystem.UpdateText(card, isDeck);
			else if (e.Button == MouseButtons.Left)
				zoomCard(card);
		}

		private bool handleCountChange(int countDelta, bool isDeck, Card card)
		{
			if (countDelta == 0)
				return false;

			if (isDeck)
				_deckEditorModel.Add(card, countDelta);
			else
				_collectionModel.Add(card, countDelta);

			return true;
		}

		private void countInput((int CountDelta, bool IsDeck, Card Card) e)
		{
			if (e.CountDelta != 0)
				handleCountChange(e.CountDelta, e.IsDeck, e.Card);
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



		private void zoomCard(Card card)
		{
			if (!card.HasImage(Ui))
				return;

			_ctsLifetime.Token.Run(async token =>
			{
				await _formZoom.LoadImages(card, Ui);
				_parent.Invoke(delegate { _formZoom.ShowImages(); });
			});
		}

		private void draggedLikeClick(Card card)
		{
			// a click with unintended mouse micro-movement was considered as starting drag-n-drop
			// for greater user friendliness lets handle it as a normal click - show zoomed card

			if (card != null)
				zoomCard(card);
		}

		private static void selectionStarted(object sender, HitInfo hitInfo, CancelEventArgs cancelArgs)
		{
			if (hitInfo.IsOverImage() || hitInfo.CustomButtonIndex >= 0)
				cancelArgs.Cancel = true;
		}



		public void Dispose()
		{
			_ctsLifetime.Cancel();

			_viewCards.MouseLeave -= gridMouseLeave;
			_viewDeck.MouseLeave -= gridMouseLeave;

			_viewCards.MouseMove -= gridMouseMove;
			_viewDeck.MouseMove -= gridMouseMove;

			_viewCards.MouseClicked -= gridMouseClick;
			_viewDeck.MouseClicked -= gridMouseClick;

			_draggingSubsystem.DraggedLikeClick -= draggedLikeClick;
			_draggingSubsystem.DragRemoved -= dragRemoved;
			_draggingSubsystem.DragAdded -= dragAdded;
			_viewCards.SelectionStarted -= selectionStarted;

			_countInputSubsystem.Input -= countInput;

			Disposed?.Invoke(this, EventArgs.Empty);
		}

		public ISite Site { get; set; }
		public event EventHandler Disposed;

		public UiModel Ui { get; set; }

		private Cursor _textSelectionCursor;
		private Cursor _zoomCursor;

		private readonly LayoutViewControl _viewCards;
		private readonly LayoutViewControl _viewDeck;
		private readonly Cursor _cursor;
		private readonly DeckEditorModel _deckEditorModel;
		private readonly CollectionEditorModel _collectionModel;
		private readonly DraggingSubsystem _draggingSubsystem;
		private readonly CountInputSubsystem _countInputSubsystem;
		private readonly FormZoom _formZoom;
		private readonly FormMain _parent;
		private readonly CancellationTokenSource _ctsLifetime;
	}
}
