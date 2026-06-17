namespace ItemChecklist.UI
{
    /// <summary>
    /// Render a single-line label without word-wrap. CK's PugFont word-wrap
    /// IndexOutOfRange-crashes on long (localised) labels — aborting ShowUI — so
    /// every header/option/row label sets <c>maxWidth = 0f</c> before
    /// <c>Render</c>. This folds that repeated pair into one call. Null-safe.
    /// </summary>
    public static class PugTextExtensions
    {
        public static void RenderNoWrap(this PugText text, string s)
        {
            if (text == null) return;
            text.maxWidth = 0f;
            text.Render(s);
        }
    }
}
