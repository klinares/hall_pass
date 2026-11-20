namespace WorldTour.Core.Data
{
    public enum Severity { Error = 0, Warning = 1 }

    /// <summary>
    /// One problem found in a data file, written for the owner. An error means the file can't be used as it is;
    /// a warning means the game still loads it but something will look off.
    /// </summary>
    public sealed class Finding
    {
        public Severity Severity;
        public string File;   // "weeks.json" or "math_config.json"
        public int Line;      // 0 when there is no place in the file (a rule checked on a model built in code)
        public int Column;    // 0 unless it is a syntax problem
        public string Message;

        public Finding(Severity severity, string file, int line, int column, string message)
        {
            Severity = severity;
            File = file;
            Line = line;
            Column = column;
            Message = TextRules.Visible(message);   // a pasted NBSP or zero-width space shows as U+00A0, not a gap
        }

        public bool IsError => Severity == Severity.Error;

        /// <summary>"weeks.json, line 14: ..." as the owner reads it.</summary>
        public override string ToString()
        {
            string where = File ?? "";
            if (Line > 0) where += (where.Length > 0 ? ", " : "") + "line " + Line + (Column > 0 ? ", column " + Column : "");
            return (where.Length > 0 ? where + ": " : "") + Message;
        }

        /// <summary>The comparable form used by the C#/Node parity test: "E|14|0|message".</summary>
        public string ParityKey => (IsError ? "E" : "W") + "|" + Line + "|" + Column + "|" + Message;
    }
}
