using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace DnDSweeper
{
    /// <summary>
    /// Custom struct defining 2D positions for DnD board.
    /// </summary>
    struct position
    {

        public int x;
        public int y;

        public position(int x, int y)
        {
            this.x = x; this.y = y;
        }

        public override string ToString()
        {
            return "(" + x.ToString() + "; " + y.ToString() + ")";
        }

        public static bool operator ==(position a, position b)
        {
            return a.x == b.x && a.y == b.y;
        }
        public static bool operator !=(position a, position b)
        {
            return a.x != b.x || a.y != b.y;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj == null) return false;
            if (obj is int[]) return ((int[])obj).Length == 2 && ((int[])obj)[0] == this.x && ((int[])obj)[1] == this.y;
            if (!(obj is position)) return false;
            position a = (position)obj;
            return a.x == x && a.y == y;
        }

        public override int GetHashCode()
        {
            return Tuple.Create(this.x, this.y).GetHashCode();
        }
    }

    enum CELL_STATE
    {
        PLAYER,
        EXIT,
        ENTRANCE,
        BOMB,
        VISITED,
        VISIBLE_OBJECT,
        EMPTY
    }

    enum GAME_STATE
    {
        RUNNING,
        WIN,
        LOSS
    }

    /// <summary>
    /// Utilitary static class for Dnd.
    /// </summary>
    internal static class DndUtil
    {
        /// <summary>
        /// Dictionary for converting states to char definitions. Provides support for game info visibility.
        /// </summary>
        /// <param name="state">Cell state.</param>
        /// <param name="show_game_info">Bool visibility parameter.</param>
        /// <returns>Char definition.</returns>
        /// <exception cref="ArgumentException">If there will be another state declared and there would be no definition.</exception>
        public static char GetCharDefinitionFromCellState(CELL_STATE state, GAME_STATE game_state = GAME_STATE.RUNNING)
        {
            switch (state)
            {
                case CELL_STATE.PLAYER:
                    return 'A';
                case CELL_STATE.EXIT:
                    return game_state != GAME_STATE.RUNNING ? '$' : '.';
                case CELL_STATE.ENTRANCE:
                    return '>';
                case CELL_STATE.BOMB:
                    return game_state != GAME_STATE.RUNNING ? 'x' : '.';
                case CELL_STATE.VISITED:
                    return '~';
                case CELL_STATE.VISIBLE_OBJECT:
                    return '?';
                case CELL_STATE.EMPTY:
                    return '.';
                default:
                    throw new ArgumentException("Unspecified state.");
            }
        }
    }

    internal static class DndConsoleUtil
    {
        /// <summary>
        /// "Clear" console doesn't really clear it, but adds multiple line breaks to simulate it.
        /// </summary>
        public static void ClearConsole()
        {
            Console.WriteLine("\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n");
        }
        /// <summary>
        /// Picking a positive integer value for a game. Checks for bad input.
        /// </summary>
        /// <param name="prompt">Variable prompt.</param>
        /// <param name="value">Out variable.</param>
        /// <param name="default_value">Default value when input is empty.</param>
        public static void PickPositiveValue(string prompt, out int value, int default_value)
        {
            while (true)
            {
                Console.Write(prompt);
                string? t = Console.ReadLine();
                if (int.TryParse(t, out value))
                {
                    if (value > 0) break;
                    else Console.WriteLine("Select a positive value.");
                }
                else
                {
                    if (String.IsNullOrEmpty(t)) { value = default_value; break; }
                    Console.WriteLine("Bad input.");
                }
            }
        }
    }

    internal static class DndConstants
    {
        public const string GAME_LOSS_MESSAGE = "You lost. This one was a bomb.";
        public const string GAME_WIN_MESSAGE = "You win! You cleared this dungeon!";

        public const int WIDTH = 12;
        public const int HEIGHT = 12;
        public const int BOMBS = 12;

        public const string WIDTH_PROMPT = "Enter the dungeon width: ";
        public const string HEIGHT_PROMPT = "Enter the dungeon height: ";
        public const string BOMBS_PROMPT = "Enter the amount of bombs: ";
    }

    internal abstract class DndObject
    {
        // delegate on aquiring, cell visibility, etc for next update
    }

    /// <summary>
    /// Class defining DnD cell.
    /// </summary>
    class DndCell
    {
        public CELL_STATE state;
        public int neighbouring_bombs;

        public char ToChar()
        {
            return DndUtil.GetCharDefinitionFromCellState(state);
        }

        public char ToChar(GAME_STATE game_state)
        {
            if (state == CELL_STATE.VISITED) return (char)(neighbouring_bombs + 48);
            return DndUtil.GetCharDefinitionFromCellState(state, game_state);
        }

        public override string ToString()
        {
            return ToChar().ToString();
        }
    }

    /// <summary>
    /// DnD board.
    /// </summary>
    class DndBoard
    {
        GAME_STATE game_state;

        DndCell[,] board;

        position player;
        position entrance;
        position exit;
        position compass;

        /// <summary>
        /// Player position.
        /// </summary>
        public position Player
        {
            get
            {
                return player;
            }
        }

        public bool IsGameRunning
        {
            get
            {
                return game_state == GAME_STATE.RUNNING;
            }
        }

        public bool HasCompass = false;

        position[] bombs;

        Collection<position> taken = new Collection<position>();
        Collection<position> visited = new Collection<position>();

        int width; int height;
        Random rnd;

        public bool won = false;
        public bool lost = false;

        /// <summary>
        /// DnD board construction.
        /// </summary>
        /// <param name="x">Width of the board.</param>
        /// <param name="y">Height of the board.</param>
        /// <param name="bombs">Amount of bombs.</param>
        public DndBoard(int y, int x, int bombs)
        {
            rnd = new Random();
            this.width = x;
            this.height = y;
            this.board = new DndCell[x, y];
            this.bombs = new position[bombs];
            FillBoard();
            SetupPositions();
            SetupBombs(bombs);
            game_state = GAME_STATE.RUNNING;
        }

        /// <summary>
        /// Sets up bombs in unique locations.
        /// </summary>
        /// <param name="amount">Amount of bombs.</param>
        void SetupBombs(int amount)
        {
            int count = 0;
            while (count != amount)
            {
                position t = GetUniquePosition();
                bombs[count] = t;
                count++;
                board[t.x, t.y].state = CELL_STATE.BOMB;

            }
        }

        /// <summary>
        /// Fills board with chars marking empty rooms.
        /// </summary>
        void FillBoard()
        {
            for (int i = 0; i < board.GetLength(0); i++)
            {
                for (int j = 0; j < board.GetLength(1); j++)
                {
                    board[i, j] = new DndCell();
                    board[i, j].state = CELL_STATE.EMPTY;
                }
            }
        }

        /// <summary>
        /// Defines entrance and exit and places them on board.
        /// </summary>
        void SetupPositions()
        {
            player = GetUniquePosition();
            entrance = new position(player.x, player.y);

            //assume player's 0 bomb position by taking the spaces around the entrance.
            taken.Add(new position(entrance.x + 1, entrance.y));
            taken.Add(new position(entrance.x - 1, entrance.y));
            taken.Add(new position(entrance.x, entrance.y + 1));
            taken.Add(new position(entrance.x, entrance.y - 1));

            //there is a hidden bug. if we assume that the player position is in the corner of by wall, then one or two positions are irrelevant and take space in determining whether there are empty cells or not. since this game needs at least path's worth of empty spaces, it won't come to this, but still.

            compass = GetUniquePosition();
            exit = GetUniquePosition();


            board[entrance.x, entrance.y].state = CELL_STATE.ENTRANCE;
            board[exit.x, exit.y].state = CELL_STATE.EXIT;
            board[compass.x, compass.y].state = CELL_STATE.VISIBLE_OBJECT;
        }

        position GetUniquePosition()
        {
            //if the count of taken cells is equal or bigger than the cell count...
            if (taken.Count >= width * height)
                //this means that we are out of empty cells.
                throw new ArgumentOutOfRangeException("There are no more empty cells.");
            position t;
            do { t = new position(rnd.Next(width), rnd.Next(height)); } while (taken.Contains(t));
            taken.Add(t);
            return t;
        }

        /// <summary>
        /// Gets a Minesweeper number of neighbouring amount of bombs from player's position.
        /// </summary>
        /// <returns>Number of neighbouring bombs.</returns>
        public int GetNeighborNumber() //defaults to player position
        {
            return GetNeighborNumber(player);
        }

        /// <summary>
        /// Gets a Minesweeper number of neighbouring amount of bombs.
        /// </summary>
        /// <param name="pos">Position of checking.</param>
        /// <returns>Number of neighbouring bombs.</returns>
        int GetNeighborNumber(position pos)
        {
            position[] positions = new position[4];
            positions[0] = new position(pos.x, pos.y - 1); // north
            positions[1] = new position(pos.x, pos.y + 1); // south
            positions[2] = new position(pos.x - 1, pos.y); // west
            positions[3] = new position(pos.x + 1, pos.y); // east

            int count = 0;

            foreach (position item in positions)
            {
                if (bombs.Contains(item)) count++;
            }
            return count;
        }

        /// <summary>
        /// Calculates closest distance from player to exit. Would be used as a powerup.
        /// </summary>
        /// <returns>Closest distance between player and exit.</returns>
        public int GetClosestDistance() //defaults to player position & exit
        {
            return GetClosestDistance(exit, player);
        }

        /// <summary>
        /// Calculates closest distance from player to exit. Would be used as a powerup.
        /// </summary>
        /// <param name="a">First position.</param>
        /// <param name="b">Second position.</param>
        /// <returns>Closest distance between two points.</returns>
        int GetClosestDistance(position a, position b)
        {
            return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
        }

        /// <summary>
        /// Method for moving player across the board.
        /// </summary>
        /// <param name="input">String input.</param>
        /// <exception cref="ArgumentException">Input should be defined or destroyed.</exception>
        public void Move(ConsoleKey key)
        {
            switch (key)
            {
                //north, up
                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                    if (player.x != 0)
                        player.x--;
                    break;
                //south, down
                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                    if (player.x != width - 1)
                        player.x++;
                    break;
                //west, left
                case ConsoleKey.LeftArrow:
                case ConsoleKey.A:
                    if (player.y != 0)
                        player.y--;
                    break;
                //east, right
                case ConsoleKey.RightArrow:
                case ConsoleKey.D:
                    if (player.y != height - 1)
                        player.y++;
                    break;

                default: throw new ArgumentException("Bad direction.");
            }
            //if the visited cell state priority is empty...
            if (board[player.x, player.y].state > CELL_STATE.VISITED)
            {
                //change state and assign neighbour bomb number to a cell
                board[player.x, player.y].state = CELL_STATE.VISITED;
                board[player.x, player.y].neighbouring_bombs = GetNeighborNumber(player);
            }
            visited.Add(player);
            CheckCellOnPlayerArrival();
        }

        void CheckCellOnPlayerArrival()
        {
            if (player == exit) DeclareWin();
            if (bombs.Contains(player)) DeclareLoss();
            if (player == compass) { HasCompass = true; board[compass.x, compass.y].state = CELL_STATE.VISITED; }
        }

        public override string ToString()
        {
            string output = "";
            for (int i = 0; i < board.GetLength(0); i++)
            {
                for (int j = 0; j < board.GetLength(1); j++)
                {
                    if (i == player.x && j == player.y) output += "[" + board[i, j].ToChar(game_state) + "]";
                    else output += " " + board[i, j].ToChar(game_state) + " ";
                }
                output += "\n";
            }
            return output;
        }

        /// <summary>
        /// Declare a win, switch game state.
        /// </summary>
        void DeclareWin()
        {
            ChangeGameState(GAME_STATE.WIN, DndConstants.GAME_WIN_MESSAGE);
        }

        /// <summary>
        /// Declare loss, switch game state.
        /// </summary>
        void DeclareLoss()
        {
            ChangeGameState(GAME_STATE.LOSS, DndConstants.GAME_LOSS_MESSAGE);
        }

        void ChangeGameState(GAME_STATE game_state, string message)
        {
            this.game_state = game_state;
            DndConsoleUtil.ClearConsole();
            Console.WriteLine(message);
        }

        public void Play()
        {
            while (IsGameRunning)
            {
                DndConsoleUtil.ClearConsole();
                Console.WriteLine(this);
                Console.WriteLine($"There are {GetNeighborNumber()} bomb(-s) around the player.");
                if (HasCompass) Console.WriteLine($"Distance to exit: {GetClosestDistance()}"); else Console.WriteLine();
                Console.WriteLine();

                Move(Console.ReadKey().Key);
            }
            Console.WriteLine(this);
            Console.WriteLine("Press any button to exit the game.");
            Console.WriteLine();
            Console.ReadKey();
        }

    }

    internal class Program
    {

        static void Main(string[] args)
        {
            DndBoard board;

            int width;
            int height;
            int bombs;

            Console.WriteLine("*DND*SWEEPER");

            DndConsoleUtil.PickPositiveValue(DndConstants.WIDTH_PROMPT, out width, DndConstants.WIDTH);
            DndConsoleUtil.PickPositiveValue(DndConstants.HEIGHT_PROMPT, out height, DndConstants.HEIGHT);
            DndConsoleUtil.PickPositiveValue(DndConstants.BOMBS_PROMPT, out bombs, DndConstants.BOMBS);

            board = new DndBoard(width, height, bombs);

            board.Play();
        }
    }
}