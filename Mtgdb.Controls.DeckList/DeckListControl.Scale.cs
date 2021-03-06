﻿using System.Drawing;

namespace Mtgdb.Controls
{
	public partial class DeckListControl
	{
		public void Scale()
		{
			_labelSortStatus.ScaleDpiAuto();
			_searchBar.ScaleDpi();
			_menuFilterByDeckMode.ScaleDpi();

			scaleLayoutView(_viewDeck);

			_labelFilterByDeckMode.ScaleDpiFont();
			_textboxRename.ScaleDpiFont();
		}

		private static void scaleLayoutView(LayoutViewControl view)
		{
			view.CardCreating += (s, l) =>
			{
				// it is here because DeckListLayout field images do not depend on data
				foreach (var field in l.Fields)
					_fieldImageScaler.Setup(field);

				_cardImageScaler.Setup((DeckListLayout) l);
			};

			view.ScaleDpi(
				bmp => bmp.HalfResizeDpi(),
				transformSearchIcon: bmp => bmp.HalfResizeDpi(),
				transformCustomButtonIcon: transformCustomButtonIcon);
		}

		private static Bitmap transformCustomButtonIcon(Bitmap bmp, string field, int i) =>
			Dpi.ScalePercent > 100
				? bmp.HalfResizeDpi()
				: bmp.ResizeDpi();

		private void systemColorsChanged() =>
			_textboxRename.TouchColorProperties();



		private static readonly DpiScaler<FieldControl, Bitmap> _fieldImageScaler =
			new DpiScaler<FieldControl, Bitmap>(
				c => (Bitmap) c.Image,
				(c, bmp) => c.Image = bmp,
				bmp => bmp?.HalfResizeDpi());

		private static readonly DpiScaler<DeckListLayout, (Bitmap, Bitmap)> _cardImageScaler =
			DpiScalers.Combine(
				new DpiScaler<DeckListLayout, Bitmap>(
					c => (Bitmap) c.ImageDeckBox,
					(c, bmp) => c.ImageDeckBox = bmp,
					bmp => bmp.ResizeDpi()),
				new DpiScaler<DeckListLayout, Bitmap>(
					c => (Bitmap) c.ImageDeckBoxOpened,
					(c, bmp) => c.ImageDeckBoxOpened = bmp,
					bmp => bmp.ResizeDpi()));
	}
}