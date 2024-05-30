using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

using TextToTMPNamespace.Instance95ff074d0d044dfcbfbde02911a1f758;
namespace TextToTMPNamespace.Instance95ff074d0d044dfcbfbde02911a1f758
{
	using UnityEngine;
	using TMPro;

	internal static class TextToTMPExtensions
	{
		public static void SetTMPAlignment( this TMP_Text tmp, TextAlignmentOptions alignment )
		{
			tmp.alignment = alignment;
		}

		public static void SetTMPAlignment( this TMP_Text tmp, TextAnchor alignment )
		{
			switch( alignment )
			{
				case TextAnchor.LowerLeft: tmp.alignment = TextAlignmentOptions.BottomLeft; break;
				case TextAnchor.LowerCenter: tmp.alignment = TextAlignmentOptions.Bottom; break;
				case TextAnchor.LowerRight: tmp.alignment = TextAlignmentOptions.BottomRight; break;
				case TextAnchor.MiddleLeft: tmp.alignment = TextAlignmentOptions.Left; break;
				case TextAnchor.MiddleCenter: tmp.alignment = TextAlignmentOptions.Center; break;
				case TextAnchor.MiddleRight: tmp.alignment = TextAlignmentOptions.Right; break;
				case TextAnchor.UpperLeft: tmp.alignment = TextAlignmentOptions.TopLeft; break;
				case TextAnchor.UpperCenter: tmp.alignment = TextAlignmentOptions.Top; break;
				case TextAnchor.UpperRight: tmp.alignment = TextAlignmentOptions.TopRight; break;
				default: tmp.alignment = TextAlignmentOptions.Center; break;
			}
		}

		public static void SetTMPFontStyle( this TMP_Text tmp, FontStyles fontStyle )
		{
			tmp.fontStyle = fontStyle;
		}

		public static void SetTMPFontStyle( this TMP_Text tmp, FontStyle fontStyle )
		{
			FontStyles fontStyles;
			switch( fontStyle )
			{
				case FontStyle.Bold: fontStyles = FontStyles.Bold; break;
				case FontStyle.Italic: fontStyles = FontStyles.Italic; break;
				case FontStyle.BoldAndItalic: fontStyles = FontStyles.Bold | FontStyles.Italic; break;
				default: fontStyles = FontStyles.Normal; break;
			}

			tmp.fontStyle = fontStyles;
		}

		public static void SetTMPHorizontalOverflow( this TMP_Text tmp, HorizontalWrapMode overflow )
		{
			tmp.enableWordWrapping = ( overflow == HorizontalWrapMode.Wrap );
		}

		public static HorizontalWrapMode GetTMPHorizontalOverflow( this TMP_Text tmp )
		{
			return tmp.enableWordWrapping ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
		}

		public static void SetTMPVerticalOverflow( this TMP_Text tmp, TextOverflowModes overflow )
		{
			tmp.overflowMode = overflow;
		}

		public static void SetTMPVerticalOverflow( this TMP_Text tmp, VerticalWrapMode overflow )
		{
			tmp.overflowMode = ( overflow == VerticalWrapMode.Overflow ) ? TextOverflowModes.Overflow : TextOverflowModes.Truncate;
		}

		public static void SetTMPLineSpacing( this TMP_Text tmp, float lineSpacing )
		{
			tmp.lineSpacing = ( lineSpacing - 1 ) * 100f;
		}

		public static void SetTMPCaretWidth( this TMP_InputField tmp, int caretWidth )
		{
			tmp.caretWidth = caretWidth;
		}

		public static void SetTMPCaretWidth( this TMP_InputField tmp, float caretWidth )
		{
			tmp.caretWidth = (int) caretWidth;
		}
	}
}


namespace RuntimeInspectorNamespace
{
	public class StringField : InspectorField
	{
		public enum Mode { OnValueChange = 0, OnSubmit = 1 };

#pragma warning disable 0649
		[SerializeField]
		private BoundInputField input;
#pragma warning restore 0649

		private Mode m_setterMode = Mode.OnValueChange;
		public Mode SetterMode
		{
			get { return m_setterMode; }
			set
			{
				m_setterMode = value;
				input.CacheTextOnValueChange = m_setterMode == Mode.OnValueChange;
			}
		}

		private int lineCount = 1;
		protected override float HeightMultiplier { get { return lineCount; } }

		public override void Initialize()
		{
			base.Initialize();

			input.Initialize();
			input.OnValueChanged += OnValueChanged;
			input.OnValueSubmitted += OnValueSubmitted;
			input.DefaultEmptyValue = string.Empty;
		}

		public override bool SupportsType( Type type )
		{
			return type == typeof( string );
		}

		protected override void OnBound( MemberInfo variable )
		{
			base.OnBound( variable );

			int prevLineCount = lineCount;
			if( variable == null )
				lineCount = 1;
			else
			{
				MultilineAttribute multilineAttribute = variable.GetAttribute<MultilineAttribute>();
				if( multilineAttribute != null )
					lineCount = Mathf.Max( 1, multilineAttribute.lines );
				else if( variable.HasAttribute<TextAreaAttribute>() )
					lineCount = 3;
				else
					lineCount = 1;
			}

			if( prevLineCount != lineCount )
			{
				input.BackingField.lineType = lineCount > 1 ? TMPro.TMP_InputField.LineType.MultiLineNewline : TMPro.TMP_InputField.LineType.SingleLine;
				input.BackingField.textComponent.SetTMPAlignment( lineCount > 1 ? TMPro.TextAlignmentOptions.TopLeft : TMPro.TextAlignmentOptions.Left );

				OnSkinChanged();
			}
		}

		protected override void OnUnbound()
		{
			base.OnUnbound();
			SetterMode = Mode.OnValueChange;
		}

		private bool OnValueChanged( BoundInputField source, string input )
		{
			if( m_setterMode == Mode.OnValueChange )
				Value = input;

			return true;
		}

		private bool OnValueSubmitted( BoundInputField source, string input )
		{
			if( m_setterMode == Mode.OnSubmit )
				Value = input;

			Inspector.RefreshDelayed();
			return true;
		}

		protected override void OnSkinChanged()
		{
			base.OnSkinChanged();
			input.Skin = Skin;

			Vector2 rightSideAnchorMin = new Vector2( Skin.LabelWidthPercentage, 0f );
			variableNameMask.rectTransform.anchorMin = rightSideAnchorMin;
			( (RectTransform) input.transform ).anchorMin = rightSideAnchorMin;
		}

		public override void Refresh()
		{
			base.Refresh();

			if( Value == null )
				input.Text = string.Empty;
			else
				input.Text = (string) Value;
		}
	}
}