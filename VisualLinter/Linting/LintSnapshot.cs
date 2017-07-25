using jwldnr.VisualLinter.Helpers;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace jwldnr.VisualLinter.Linting
{
    internal class LintSnapshot : WpfTableEntriesSnapshotBase
    {
        public override int Count => _warnings.Count;
        public override int VersionNumber { get; }

        internal IEnumerable<LintWarning> Warnings => _readonlyWarnings;
        internal LintSnapshot NextSnapshot;

        private readonly string _filePath;
        private readonly IReadOnlyCollection<LintWarning> _readonlyWarnings;
        private readonly IList<LintWarning> _warnings;

        private string _projectName;

        internal LintSnapshot(string filePath, int versionNumber, IEnumerable<LintWarning> warnings)
        {
            _filePath = filePath;
            VersionNumber = versionNumber;

            _warnings = new List<LintWarning>(warnings);
            _readonlyWarnings = new ReadOnlyCollection<LintWarning>(_warnings);
        }

        public override bool CanCreateDetailsContent(int index)
        {
            // todo, when fix is available..
            // return return null != _warnings[index].Message.Fix.Text;
            return false;
        }

        public override bool TryCreateDetailsStringContent(int index, out string content)
        {
            // todo, use the lint fix to provide a more detailed description
            content = _warnings[index].Message.Message;
            return null != content;
        }

        public override bool TryGetValue(int index, string columnName, out object content)
        {
            content = null;

            if (index < 0 || _warnings.Count <= index)
                return false;

            var warning = _warnings[index];

            switch (columnName)
            {
                case StandardTableKeyNames.BuildTool:
                    // todo get analyzer name
                    content = Vsix.Name;
                    return true;

                case StandardTableKeyNames.Column:
                    var position = warning.Span.Start;
                    var line = position.GetContainingLine();
                    content = position.Position - line.Start.Position;
                    return true;

                case StandardTableKeyNames.DocumentName:
                    content = _filePath;
                    return null != content;

                case StandardTableKeyNames.ErrorCodeToolTip:
                case StandardTableKeyNames.HelpLink:
                    content = GetRuleUrl(warning.Message.IsFatal, warning.Message.RuleId);
                    return null != content;

                case StandardTableKeyNames.ErrorCode:
                    content = GetErrorCode(warning.Message.IsFatal, warning.Message.RuleId);
                    return true;

                case StandardTableKeyNames.ErrorSeverity:
                    content = GetErrorCategory(warning.Message.IsFatal);
                    return true;

                case StandardTableKeyNames.ErrorSource:
                    content = warning.Message.Source;
                    return null != content;

                case StandardTableKeyNames.Line:
                    content = warning.Span.Start.GetContainingLine().LineNumber;
                    return true;

                case StandardTableKeyNames.ProjectName:
                    if (string.IsNullOrEmpty(_projectName))
                        _projectName = VsixHelper.GetProjectName(_filePath);
                    content = _projectName;
                    return null != content;

                case StandardTableKeyNames.Text:
                    content = warning.Message.Message;
                    return null != content;

                default:
                    return false;
            }
        }

        private static __VSERRORCATEGORY GetErrorCategory(bool isFatal)
        {
            return isFatal
                ? __VSERRORCATEGORY.EC_ERROR
                : __VSERRORCATEGORY.EC_WARNING;
        }

        private static string GetErrorCode(bool isFatal, string ruleId)
        {
            return isFatal
                ? null
                : $"({ruleId})";
        }

        private static string GetRuleUrl(bool isFatal, string ruleId)
        {
            return isFatal
                ? null
                : $"http://eslint.org/docs/rules/{ruleId}";
        }
    }
}