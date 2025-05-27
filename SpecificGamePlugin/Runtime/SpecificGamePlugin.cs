using System;
using SharedLogic;

namespace SpecificGamePlugin
{
    public enum CellState { Empty, Player, Computer }
    public enum GameResult { InProgress, Draw, Win, Defeat }

    // Player profile for storing game state
    public class ConnectFourProfile : UserProfile
    {
        // In Connect Four, the board usually has 6 rows and 7 columns.
        // The profile stores the full state of the game board.
        public CellState[,] Board = new CellState[6, 7];

        // Stores the game status - still playing or already finished with some result.
        public GameResult Result = GameResult.InProgress;

        // Seed for deterministic random number generator.
        // This is crucial: we need to get the same random numbers on client and server,
        // so we can't use regular Random. We need to sync its seed,
        // and cannot use System.Random as its results depend on the platform.
        public int Seed;

        // Player match statistics
        public int VictoryCount = 0;
        public int DefeatCount = 0;
    }

    public class DropDiscCommand : ICommandData
    {
        public int Column { get; set; } // The column where the player wants to drop the disc
    }

    public class NewGameCommand : ICommandData { } // Command to start a new game

    // The main plugin class - now inherits from CommandDispatcher.
    // The backend finds this type via reflection and instantiates it as the command handler.
    public class ConnectFourCommandProcessor : GameCommandProcessor
    {
        //we usually have some events during processing of command which we want to show on UI
        public event Action OnVictory = () => { };
        public event Action OnDefeat = () => { };
        public event Action OnDraw = () => { };
        public ConnectFourCommandProcessor()
        {
            // The new game command clears the board and resets the game status.
            // Victory and defeat stats remain unchanged.
            RegisterHandler<ConnectFourProfile, NewGameCommand>((userProfile, cmd) =>
            {
                userProfile.Board = new CellState[6, 7];
                userProfile.Result = GameResult.InProgress;
            });

            // This is the main command handler - dropping a disc.
            // The game is always against the computer in this example.
            // Inside this handler:
            // * The board state is changed according to the dropped disc
            // * Victory/draw/defeat conditions are checked
            // * The computer makes its move using a simple algorithm
            // Command handling can use methods in ConnectFourDispatcher (e.g., DropDisc, CheckVictory,
            // ChooseBestMove, etc.) and additional classes (e.g., DeterministicRandom)
            // No UnityEngine.dll code is used here, as we want all this code to run identically on the backend
            // (where Unity is not available).
            RegisterHandler<ConnectFourProfile, DropDiscCommand>((userProfile, cmd) =>
            {
                if (userProfile.Result != GameResult.InProgress) return; // Ignore command if game is finished

                // Player's move
                var row = DropDisc(userProfile.Board, cmd.Column, CellState.Player);
                if (row == -1)
                    throw new Exception("Column full");

                // Did the player win?
                if (CheckVictory(userProfile.Board, row, cmd.Column, CellState.Player))
                {
                    userProfile.Result = GameResult.Win;
                    userProfile.VictoryCount++;
                    OnVictory?.Invoke();
                    return;
                }

                // Computer's move (rule-based AI)
                var rand = new DeterministicRandom(userProfile.Seed++); // Deterministic random generator
                int baseCol = rand.Next(0, 7); // Starting point for fallback column choice
                int aiCol = ChooseBestMove(userProfile.Board, baseCol);
                int aiRow = DropDisc(userProfile.Board, aiCol, CellState.Computer);

                // Did the computer win?
                if (CheckVictory(userProfile.Board, aiRow, aiCol, CellState.Computer))
                {
                    userProfile.Result = GameResult.Defeat;
                    userProfile.DefeatCount++;
                    OnDefeat?.Invoke();
                }

                // Check for draw
                if (userProfile.Result == GameResult.InProgress && IsBoardFull(userProfile.Board))
                {
                    userProfile.Result = GameResult.Draw;
                    OnDraw?.Invoke();
                }
            });
        }

        // Places a disc into the specified column. Returns the row index, or -1 if the column is full.
        private int DropDisc(CellState[,] board, int col, CellState who)
        {
            for (int row = 5; row >= 0; row--)
            {
                if (board[row, col] == CellState.Empty)
                {
                    board[row, col] = who;
                    return row;
                }
            }
            return -1;
        }

        // Checks if the last move resulted in a win.
        private bool CheckVictory(CellState[,] board, int row, int col, CellState who)
        {
            return Count(board, row, col, -1, 0, who) + Count(board, row, col, 1, 0, who) > 2 || // vertical
                   Count(board, row, col, 0, -1, who) + Count(board, row, col, 0, 1, who) > 2 || // horizontal
                   Count(board, row, col, -1, -1, who) + Count(board, row, col, 1, 1, who) > 2 || // diagonal \
                   Count(board, row, col, -1, 1, who) + Count(board, row, col, 1, -1, who) > 2;   // diagonal /
        }

        // Counts the number of discs in a row in one direction
        private int Count(CellState[,] board, int row, int col, int dx, int dy, CellState who)
        {
            int count = 0;
            int r = row + dx;
            int c = col + dy;
            while (r >= 0 && r < 6 && c >= 0 && c < 7 && board[r, c] == who)
            {
                count++;
                r += dx;
                c += dy;
            }
            return count;
        }

        // Checks if the board is full
        private bool IsBoardFull(CellState[,] board)
        {
            for (int col = 0; col < 7; col++)
            {
                if (board[0, col] == CellState.Empty)
                    return false;
            }
            return true;
        }

        // Chooses the best move based on priority: win, block the player, center, fallback
        private int ChooseBestMove(CellState[,] board, int baseCol)
        {
            // 1. Winning move
            for (int col = 0; col < 7; col++)
            {
                int row = SimulateDrop(board, col);
                if (row != -1 && CheckVictoryWithSim(board, row, col, CellState.Computer))
                    return col;
            }

            // 2. Block the player
            for (int col = 0; col < 7; col++)
            {
                int row = SimulateDrop(board, col);
                if (row != -1 && CheckVictoryWithSim(board, row, col, CellState.Player))
                    return col;
            }

            // 3. Center column is more strategic
            if (board[0, 3] == CellState.Empty) return 3;

            // 4. Random valid move starting from baseCol
            for (int i = 0; i < 7; i++)
            {
                int col = (baseCol + i) % 7;
                if (board[0, col] == CellState.Empty)
                    return col;
            }

            return 0; // fallback in case the board is full
        }

        // Simulates dropping a disc without modifying the board
        private int SimulateDrop(CellState[,] board, int col)
        {
            for (int row = 5; row >= 0; row--)
                if (board[row, col] == CellState.Empty)
                    return row;
            return -1;
        }

        // Checks for victory with a temporary disc placement (without changing the board permanently)
        private bool CheckVictoryWithSim(CellState[,] board, int row, int col, CellState who)
        {
            board[row, col] = who;
            bool result = CheckVictory(board, row, col, who);
            board[row, col] = CellState.Empty;
            return result;
        }

        // Create a default player profile
        public override UserProfile CreateDefaultProfile(Guid userId)
        {
            return new ConnectFourProfile
            {
                _id = userId,
                Board = new CellState[6, 7],
                Seed = userId.GetHashCode() // each user gets a unique initial seed.
            };
        }
    }

    // Simple deterministic random number generator
    // Necessary for getting the same random numbers on client and server
    // when executing the same command on the same profile state.
    public class DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(int seed)
        {
            _state = (uint)seed;
            if (_state == 0) _state = 1;
        }

        public int Next(int min, int max)
        {
            return min + (int)(NextUInt() % (uint)(max - min));
        }

        private uint NextUInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }
    }
}
