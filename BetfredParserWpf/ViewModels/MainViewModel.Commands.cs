using System.Windows.Input;
using BetfredParserWpf.Commands;

namespace BetfredParserWpf.ViewModels
{
    public partial class MainViewModel
    {
        private DelegateCommand _exitCommand;

        public ICommand ExitCommand
        {
            get
            {
                if (_exitCommand == null)
                {
                    _exitCommand = new DelegateCommand(Exit);
                }
                return _exitCommand;
            }
        }
    }
}