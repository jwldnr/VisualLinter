using jwldnr.VisualLinter.Helpers;
using jwldnr.VisualLinter.Linting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace jwldnr.VisualLinter.Tagging
{
    internal class LinterTagger : ITagger<IErrorTag>, IDisposable
    {
        internal SnapshotFactory Factory { get; }
        internal string FilePath { get; private set; }
        internal LinterSnapshot Snapshot { get; set; }

        private readonly ITextBuffer _buffer;
        private readonly ITextDocument _document;
        private readonly Linter _linter;
        private readonly TaggerProvider _provider;

        private ITextSnapshot _currentSnapshot;
        private NormalizedSnapshotSpanCollection _dirtySpans;

        internal LinterTagger(
            TaggerProvider provider,
            Linter linter,
            ITextBuffer buffer,
            ITextDocument document)
        {
            _provider = provider;
            _linter = linter;
            _buffer = buffer;
            _document = document;

            _currentSnapshot = buffer.CurrentSnapshot;
            _dirtySpans = new NormalizedSnapshotSpanCollection();

            FilePath = document.FilePath;
            Factory = new SnapshotFactory(new LinterSnapshot(FilePath, 0, new List<LinterMarker>()));

            Initialize();
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public void Dispose()
        {
            _document.FileActionOccurred -= OnFileActionOccurred;
            _buffer.ChangedLowPriority -= OnBufferChange;

            _provider.RemoveTagger(this);
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (0 == spans.Count || null == Snapshot || false == Snapshot.Markers.Any())
                return Enumerable.Empty<ITagSpan<IErrorTag>>();

            try
            {
                return Snapshot.Markers
                    .Where(marker => spans.IntersectsWith(new NormalizedSnapshotSpanCollection(marker.Span)))
                    .Select(marker => new TagSpan<IErrorTag>(marker.Span, new LinterTag(marker.Message)));
            }
            catch (Exception e)
            {
                OutputWindowHelper.WriteLine(e.Message);
            }

            return Enumerable.Empty<ITagSpan<IErrorTag>>();
        }

        internal void UpdateMessages(IEnumerable<LinterMessage> messages)
        {
            var oldSnapshot = Factory.CurrentSnapshot;

            var markers = GetMessageRanges(messages).Where(IsValidRange).Select(CreateMarker);
            var newSnapshot = new LinterSnapshot(FilePath, oldSnapshot.VersionNumber + 1, markers);

            SnapToNewSnapshot(newSnapshot);
        }

        private async Task AnalyzeAsync(string filePath)
        {
            if (null == VsixHelper.GetProjectItem(filePath))
                return;

            var source = _currentSnapshot.GetText();
            var messages = await LintAsync(filePath, source);

            UpdateMessages(messages);
        }

        private LinterMarker CreateMarker(LinterMessage message)
        {
            var start = new SnapshotPoint(_currentSnapshot, message.Range.StartColumn);
            var end = new SnapshotPoint(_currentSnapshot, message.Range.EndColumn);

            return new LinterMarker(new SnapshotSpan(start, end), message);
        }

        private MessageRange GetMessageRange(LinterMessage message)
        {
            try
            {
                var lineNumber = message.Line - 1;
                var column = message.Column - 1;

                if (lineNumber < 0)
                    lineNumber = 0;

                var lineCount = _currentSnapshot.LineCount;

                if (lineNumber > lineCount)
                    throw new ArgumentOutOfRangeException($"Line number ({lineNumber}) greater than line count ({lineCount})");

                var line = _currentSnapshot.GetLineFromLineNumber(lineNumber);
                var lineText = line.GetText();

                int endColumn = line.End;
                var startColumn = line.Start.Add(column);

                if (message.EndColumn.HasValue)
                {
                    var value = message.EndColumn.Value - 1;
                    var length = value - column;
                    endColumn = startColumn.Add(length);
                }
                else
                {
                    var match = RegexHelper.GetWord(lineText.Substring(column));
                    if (match.Success)
                        endColumn = startColumn.Add(match.Index).Add(match.Length);
                }

                if (startColumn > endColumn)
                    throw new ArgumentOutOfRangeException($"Start column ({startColumn.Position}) greater than end column ({endColumn}) for line {lineNumber}");

                return new MessageRange
                {
                    LineNumber = lineNumber,
                    StartColumn = startColumn,
                    EndColumn = endColumn
                };
            }
            catch (Exception e)
            {
                OutputWindowHelper.WriteLine(e.Message);
            }

            return null;
        }

        private IEnumerable<LinterMessage> GetMessageRanges(IEnumerable<LinterMessage> messages)
        {
            foreach (var message in messages)
            {
                message.Range = GetMessageRange(message);
                yield return message;
            }
        }

        private Task Initialize()
        {
            _document.FileActionOccurred += OnFileActionOccurred;
            _buffer.ChangedLowPriority += OnBufferChange;

            _provider.AddTagger(this);

            return AnalyzeAsync(FilePath);
        }

        private bool IsValidRange(LinterMessage message)
        {
            var range = message.Range;

            if (null == range)
                return false;

            return range.LineNumber >= 0 && range.LineNumber <= _currentSnapshot.LineCount &&
                range.EndColumn <= _currentSnapshot.Length;
        }

        private Task<IEnumerable<LinterMessage>> LintAsync(string filePath, string source)
        {
            return _linter.LintAsync(filePath, source);
        }

        private void OnBufferChange(object sender, TextContentChangedEventArgs e)
        {
            UpdateDirtySpans(e);

            var newSnapshot = TranslateWarningSpans();

            SnapToNewSnapshot(newSnapshot);
        }

        private async void OnFileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if (0 != (e.FileActionType & FileActionTypes.DocumentRenamed))
            {
                _provider.Rename(FilePath, e.FilePath);
                FilePath = e.FilePath;
            }
            else if (0 != (e.FileActionType & FileActionTypes.ContentSavedToDisk))
            {
                await AnalyzeAsync(FilePath);
            }
        }

        private void SnapToNewSnapshot(LinterSnapshot snapshot)
        {
            var factory = Factory;

            factory.UpdateResults(snapshot);

            _provider.UpdateAllSinks(factory);

            UpdateWarnings(_currentSnapshot, snapshot);

            Snapshot = snapshot;
        }

        private LinterSnapshot TranslateWarningSpans()
        {
            var oldSnapshot = Factory.CurrentSnapshot;

            var newWarnings = oldSnapshot.Markers
                .Select(marker => marker.CloneAndTranslateTo(marker, _currentSnapshot))
                .Where(clone => null != clone);

            return new LinterSnapshot(FilePath, oldSnapshot.VersionNumber + 1, newWarnings);
        }

        private void UpdateDirtySpans(TextContentChangedEventArgs e)
        {
            _currentSnapshot = e.After;

            var newDirtySpans = _dirtySpans.CloneAndTrackTo(e.After, SpanTrackingMode.EdgeInclusive);

            newDirtySpans = e.Changes
                .Aggregate(newDirtySpans, (current, change) => NormalizedSnapshotSpanCollection.Union(
                    current, new NormalizedSnapshotSpanCollection(e.After, change.NewSpan)));

            _dirtySpans = newDirtySpans;
        }

        private void UpdateWarnings(ITextSnapshot currentSnapshot, LinterSnapshot snapshot)
        {
            var oldSnapshot = Snapshot;

            var handler = TagsChanged;
            if (null == handler)
                return;

            var start = int.MaxValue;
            var end = int.MinValue;

            if (null != oldSnapshot && 0 < oldSnapshot.Count)
            {
                start = oldSnapshot.Markers.First().Span.Start
                    .TranslateTo(currentSnapshot, PointTrackingMode.Negative);
                end = oldSnapshot.Markers.Last().Span.End
                    .TranslateTo(currentSnapshot, PointTrackingMode.Positive);
            }

            if (0 < snapshot.Count)
            {
                start = Math.Min(start, snapshot.Markers.First().Span.Start.Position);
                end = Math.Max(end, snapshot.Markers.Last().Span.End.Position);
            }

            if (start >= end)
                return;

            handler(this, new SnapshotSpanEventArgs(new SnapshotSpan(
                currentSnapshot, Span.FromBounds(start, end))));
        }
    }
}