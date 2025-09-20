using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "DecimalValidator", menuName = "TMP Validators/Decimal Validator")]
public class DecimalValidator : TMP_InputValidator
{
    public override char Validate(ref string text, ref int pos, char ch)
    {
        // Chỉ cho số
        if (char.IsDigit(ch))
        {
            text = text.Insert(pos, ch.ToString());
            pos++;
            return ch;
        }

        // Chỉ cho phép 1 dấu phân cách '.' hoặc ','
        if (ch == '.' || ch == ',')
        {
            if (text.Contains(".") || text.Contains(","))
                return '\0'; // đã có rồi thì chặn

            text = text.Insert(pos, "."); // normalize về '.'
            pos++;
            return '.';
        }

        return '\0'; // chặn mọi ký tự khác (kể cả '-')
    }
}