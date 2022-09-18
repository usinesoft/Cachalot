using AdminConsole.ConsoleUtils.ConsoleActions;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AdminConsole.ConsoleUtils
{
    public static class ConsoleExt
    {
        private static readonly object LockObj = new object();
        private static int _maxLineLength;
        internal static readonly PreviousLineBuffer PreviousLineBuffer = new PreviousLineBuffer();

        private static readonly Dictionary<ConsoleModifiers, IConsoleAction> DefaultConsoleActions =
            new Dictionary<ConsoleModifiers, IConsoleAction>();

        private static readonly Dictionary<ConsoleModifiers, Dictionary<ConsoleKey, IConsoleAction>> ConsoleActions =
            new Dictionary<ConsoleModifiers, Dictionary<ConsoleKey, IConsoleAction>>();

        private static string _currentLine = string.Empty;

        static ConsoleExt()
        {
            ResetConsoleBehaviour();
        }

        internal static string CurrentLine
        {
            get => _currentLine;
            set
            {
                if (_currentLine == value)
                    return;
                var prevLength = _currentLine.Length;
                _currentLine = value;
                if (value.Length > prevLength)
                    UpdateBufferWidth();
                UpdateBuffer();
            }
        }

        public static LineState LineState => new LineState(CurrentLine, Console.CursorLeft);

        public static string ReadLine()
        {
            while (true)
            {
                var result = ReadKey();
                if (result.Key == ConsoleKey.Enter)
                    return result.LineBeforeKeyPress.Line;
            }
        }

        public static KeyPressResult ReadKey()
        {
            return ReadKey(false);
        }

        public static KeyPressResult ReadKey(bool intercept)
        {
            while (true)
            {
                var lineStateBefore = LineState;
                var keyInfo = Console.ReadKey(true);
                if (!intercept)
                    SimulateKeyPress(keyInfo);

                return new KeyPressResult(keyInfo, lineStateBefore, LineState);
            }
        }

        public static void SimulateKeyPress(char keyChar)
        {
            ConsoleKey consoleKey;
            if (!ConsoleKeyConverter.TryParseChar(keyChar, out consoleKey))
                return;
            SimulateKeyPress(new ConsoleKeyInfo(keyChar, consoleKey, false, false, false));
        }

        public static void SimulateKeyPress(ConsoleKey consoleKey)
        {
            SimulateKeyPress(new ConsoleKeyInfo((char)consoleKey, consoleKey, false, false, false));
        }

        public static void SimulateKeyPress(ConsoleKeyInfo keyInfo)
        {
            lock (LockObj)
            {
                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    StartNewLine();
                    return;
                }

                IConsoleAction action;
                Dictionary<ConsoleKey, IConsoleAction> consoleKeyMapping;
                if (ConsoleActions.TryGetValue(keyInfo.Modifiers, out consoleKeyMapping))
                    if (consoleKeyMapping.TryGetValue(keyInfo.Key, out action))
                    {
                        action.Execute(new ConsoleExtInstance(), keyInfo);
                        return;
                    }

                if (DefaultConsoleActions.TryGetValue(keyInfo.Modifiers, out action))
                    action.Execute(new ConsoleExtInstance(), keyInfo);
            }
        }

        public static void SetLine(string input)
        {
            lock (LockObj)
            {
                CurrentLine = input;
                Console.CursorLeft = CurrentLine.Length;
            }
        }

        public static void ClearLine()
        {
            lock (LockObj)
            {
                Console.CursorLeft = 0;
                CurrentLine = string.Empty;
                _maxLineLength = 0;
            }
        }

        public static void StartNewLine()
        {
            lock (LockObj)
            {
                Console.CursorLeft = 0;
                Console.WriteLine(CurrentLine);
                PreviousLineBuffer.AddLine(CurrentLine);
                _currentLine = string.Empty;
                _maxLineLength = 0;
            }
        }

        public static void PrependLine(string line)
        {
            lock (LockObj)
            {
                var currentLine = CurrentLine;
                var cursorPos = Console.CursorLeft;
                SetLine("");
                Console.CursorLeft = 0;
                Console.WriteLine(line);
                CurrentLine = currentLine;
                Console.CursorLeft = cursorPos;
            }
        }

        public static void ResetConsoleBehaviour()
        {
            DefaultConsoleActions.Clear();
            ConsoleActions.Clear();

            SetDefaultConsoleActionForNonCtrlModifierCombinations(new InsertCharacterAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.F1, new AutoCompleteSingleCharacterAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.F3, new AutoCompleteRestOfLineAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.F5, new CycleUpAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.F6, new IgnoreAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.F8, new AutoCompleteUsingPreviousLinesAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.Escape, new ClearLineAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.Delete, new DeleteAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.Backspace, new BackspaceAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.LeftArrow, new MoveCursorLeftAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.RightArrow, new MoveCursorRightAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.Home, new MoveCursorToBeginAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.End, new MoveCursorToEndAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.Tab, new IgnoreAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.UpArrow, new CycleUpAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.PageUp, new CycleTopAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.DownArrow, new CycleDownAction());
            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.PageDown, new CycleBottomAction());

            SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey.D8, new InsertCharacterAction());

            SetDefaultConsoleActionForCtrlModifierCombinations(new InsertCharacterAction());
            SetConsoleActionForCtrlModifierCombinations(ConsoleKey.H, new BackspaceAction());
            SetConsoleActionForCtrlModifierCombinations(ConsoleKey.Backspace, new RemovePrecedingAction());
            SetConsoleActionForCtrlModifierCombinations(ConsoleKey.LeftArrow, new MoveCursorToBeginAction());
            SetConsoleActionForCtrlModifierCombinations(ConsoleKey.RightArrow, new MoveCursorToEndAction());
            SetConsoleActionForCtrlModifierCombinations(ConsoleKey.Home, new RemovePrecedingAction());
            SetConsoleActionForCtrlModifierCombinations(ConsoleKey.End, new RemoveSucceedingAction());
        }

        public static void SetConsoleActionForNonCtrlModifierCombinations(ConsoleKey consoleKey, IConsoleAction action)
        {
            SetConsoleAction(0, consoleKey, action);
            SetConsoleAction(ConsoleModifiers.Shift, consoleKey, action);

            SetConsoleAction(ConsoleModifiers.Alt, consoleKey, action);
            SetConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Alt, consoleKey, action);
            SetConsoleAction(ConsoleModifiers.Control | ConsoleModifiers.Alt, consoleKey, action);
        }

        public static void SetConsoleActionForCtrlModifierCombinations(ConsoleKey consoleKey, IConsoleAction action)
        {
            SetConsoleAction(ConsoleModifiers.Control, consoleKey, action);
            SetConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Control, consoleKey, action);
            SetConsoleAction(ConsoleModifiers.Alt | ConsoleModifiers.Control, consoleKey, action);
            SetConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Alt | ConsoleModifiers.Control, consoleKey,
                action);
        }

        public static void SetConsoleActionForAllModifierCombinations(ConsoleKey consoleKey, IConsoleAction action)
        {
            SetConsoleAction(0, consoleKey, action);
            SetConsoleAction(ConsoleModifiers.Shift, consoleKey, action);
            SetConsoleAction(ConsoleModifiers.Alt, consoleKey, action);
            SetConsoleAction(ConsoleModifiers.Control, consoleKey, action);
            SetConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Alt, consoleKey, action);
            SetConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Control, consoleKey, action);
            SetConsoleAction(ConsoleModifiers.Alt | ConsoleModifiers.Control, consoleKey, action);
            SetConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Alt | ConsoleModifiers.Control, consoleKey,
                action);
        }

        public static void SetConsoleAction(ConsoleModifiers modifiers, ConsoleKey consoleKey, IConsoleAction action)
        {
            Dictionary<ConsoleKey, IConsoleAction> consoleKeyMapping;
            if (!ConsoleActions.TryGetValue(modifiers, out consoleKeyMapping))
            {
                consoleKeyMapping = new Dictionary<ConsoleKey, IConsoleAction>();
                ConsoleActions.Add(modifiers, consoleKeyMapping);
            }

            consoleKeyMapping[consoleKey] = action;
        }

        public static void RemoveConsoleActionForNonCtrlModifierCombinations(ConsoleKey consoleKey)
        {
            RemoveConsoleAction(0, consoleKey);
            RemoveConsoleAction(ConsoleModifiers.Shift, consoleKey);
            RemoveConsoleAction(ConsoleModifiers.Alt, consoleKey);
            RemoveConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Alt, consoleKey);
        }

        public static void RemoveConsoleActionForCtrlModifierCombinations(ConsoleKey consoleKey)
        {
            RemoveConsoleAction(ConsoleModifiers.Control, consoleKey);
            RemoveConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Control, consoleKey);
            RemoveConsoleAction(ConsoleModifiers.Alt | ConsoleModifiers.Control, consoleKey);
            RemoveConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Alt | ConsoleModifiers.Control, consoleKey);
        }

        public static void RemoveConsoleActionForAllModifierCombinations(ConsoleKey consoleKey)
        {
            RemoveConsoleAction(0, consoleKey);
            RemoveConsoleAction(ConsoleModifiers.Shift, consoleKey);
            RemoveConsoleAction(ConsoleModifiers.Alt, consoleKey);
            RemoveConsoleAction(ConsoleModifiers.Control, consoleKey);
            RemoveConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Alt, consoleKey);
            RemoveConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Control, consoleKey);
            RemoveConsoleAction(ConsoleModifiers.Alt | ConsoleModifiers.Control, consoleKey);
            RemoveConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Alt | ConsoleModifiers.Control, consoleKey);
        }

        public static void RemoveConsoleAction(ConsoleModifiers modifiers, ConsoleKey consoleKey)
        {
            Dictionary<ConsoleKey, IConsoleAction> consoleKeyMapping;
            if (!ConsoleActions.TryGetValue(modifiers, out consoleKeyMapping))
                return;
            consoleKeyMapping.Remove(consoleKey);
        }

        public static void SetDefaultConsoleActionForNonCtrlModifierCombinations(IConsoleAction action)
        {
            SetDefaultConsoleAction(0, action);
            SetDefaultConsoleAction(ConsoleModifiers.Shift, action);
            SetDefaultConsoleAction(ConsoleModifiers.Alt, action);
            SetDefaultConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Alt, action);
        }

        public static void SetDefaultConsoleActionForCtrlModifierCombinations(IConsoleAction action)
        {
            SetDefaultConsoleAction(ConsoleModifiers.Control, action);
            SetDefaultConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Control, action);
            SetDefaultConsoleAction(ConsoleModifiers.Alt | ConsoleModifiers.Control, action);
            SetDefaultConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Alt | ConsoleModifiers.Control, action);
        }

        public static void SetDefaultConsoleActionForAllModifierCombinations(IConsoleAction action)
        {
            SetDefaultConsoleAction(0, action);
            SetDefaultConsoleAction(ConsoleModifiers.Shift, action);
            SetDefaultConsoleAction(ConsoleModifiers.Alt, action);
            SetDefaultConsoleAction(ConsoleModifiers.Control, action);
            SetDefaultConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Alt, action);
            SetDefaultConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Control, action);
            SetDefaultConsoleAction(ConsoleModifiers.Alt | ConsoleModifiers.Control, action);
            SetDefaultConsoleAction(ConsoleModifiers.Shift | ConsoleModifiers.Alt | ConsoleModifiers.Control, action);
        }

        public static void SetDefaultConsoleAction(ConsoleModifiers modifiers, IConsoleAction action)
        {
            DefaultConsoleActions[modifiers] = action;
        }

        private static void UpdateBufferWidth()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.BufferWidth = Math.Max(Console.BufferWidth,
                    Math.Min(byte.MaxValue, Math.Max(_maxLineLength, CurrentLine.Length + 1)));
            }

        }

        private static void UpdateBuffer()
        {
            _maxLineLength = Math.Max(_maxLineLength, CurrentLine.Length);
            var pos = Console.CursorLeft;
            Console.CursorLeft = 0;
            Console.Write(CurrentLine.PadRight(_maxLineLength));
            Console.CursorLeft = pos;
        }
    }
}