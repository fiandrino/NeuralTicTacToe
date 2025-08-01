using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;

namespace NeuralTicTacToe
{
    public enum Player
    {
        None,
        X,
        O
    }

    public class TicTacToeGame
    {
        private Player[] board = new Player[9];
        public Player CurrentPlayer { get; private set; } = Player.X;
        public bool GameOver { get; private set; }
        public Player Winner { get; private set; } = Player.None;

        public void Reset()
        {
            board = new Player[9];
            CurrentPlayer = Player.X;
            GameOver = false;
            Winner = Player.None;
        }

        public bool MakeMove(int position)
        {
            if (position < 0 || position >= 9 || board[position] != Player.None || GameOver)
                return false;

            board[position] = CurrentPlayer;
            CheckGameOver();
            CurrentPlayer = CurrentPlayer == Player.X ? Player.O : Player.X;
            return true;
        }

        private void CheckGameOver()
        {
            // Check rows
            for (int i = 0; i < 3; i++)
            {
                if (board[i * 3] != Player.None &&
                    board[i * 3] == board[i * 3 + 1] &&
                    board[i * 3] == board[i * 3 + 2])
                {
                    GameOver = true;
                    Winner = board[i * 3];
                    return;
                }
            }

            // Check columns
            for (int i = 0; i < 3; i++)
            {
                if (board[i] != Player.None &&
                    board[i] == board[i + 3] &&
                    board[i] == board[i + 6])
                {
                    GameOver = true;
                    Winner = board[i];
                    return;
                }
            }

            // Check diagonals
            if (board[0] != Player.None &&
                board[0] == board[4] &&
                board[0] == board[8])
            {
                GameOver = true;
                Winner = board[0];
                return;
            }

            if (board[2] != Player.None &&
                board[2] == board[4] &&
                board[2] == board[6])
            {
                GameOver = true;
                Winner = board[2];
                return;
            }

            // Check draw
            if (!board.Any(p => p == Player.None))
            {
                GameOver = true;
                Winner = Player.None;
            }
        }

        public Player[] GetBoardState()
        {
            return (Player[])board.Clone();
        }

        public int[] GetValidMoves()
        {
            return board
                .Select((p, i) => (p, i))
                .Where(x => x.p == Player.None)
                .Select(x => x.i)
                .ToArray();
        }

        public string GetBoardHash()
        {
            return string.Join("", board.Select(p =>
                p == Player.X ? "X" :
                p == Player.O ? "O" : "-"));
        }
    }

    public class QLearningAgent
    {
        private readonly Random random = new Random();
        private Dictionary<string, Dictionary<int, double>> qTable = new();
        private double learningRate;
        private double discountFactor;
        private double explorationRate;

        public double ExplorationRate => explorationRate;

        public QLearningAgent(double learningRate = 0.1, double discountFactor = 0.9, double explorationRate = 1.0)
        {
            this.learningRate = learningRate;
            this.discountFactor = discountFactor;
            this.explorationRate = explorationRate;
        }

        public void SetExplorationRate(double rate)
        {
            explorationRate = Math.Max(0.001, Math.Min(1.0, rate));
        }

        public int GetMove(TicTacToeGame game)
        {
            string stateHash = GetStateHash(game.GetBoardState(), game.CurrentPlayer);
            var validMoves = game.GetValidMoves();

            if (validMoves.Length == 0)
                return -1;

            if (!qTable.ContainsKey(stateHash))
                InitializeState(stateHash, validMoves);

            if (random.NextDouble() < explorationRate)
                return validMoves[random.Next(validMoves.Length)];

            var bestMoves = qTable[stateHash]
                .Where(kv => validMoves.Contains(kv.Key))
                .OrderByDescending(kv => kv.Value)
                .ToList();

            if (bestMoves.Count == 0)
                return validMoves[random.Next(validMoves.Length)];

            double maxQ = bestMoves[0].Value;
            var topMoves = bestMoves.Where(kv => Math.Abs(kv.Value - maxQ) < 0.001).ToList();

            return topMoves[random.Next(topMoves.Count)].Key;
        }

        private void InitializeState(string stateHash, int[] validMoves)
        {
            qTable[stateHash] = validMoves.ToDictionary(
                move => move,
                move => move == 4 ? 0.5 : (move % 2 == 0 ? 0.3 : 0.1)
            );
        }

        public void UpdateQValue(Player[] state, Player player, int move, double reward, Player[] nextState)
        {
            string stateHash = GetStateHash(state, player);
            if (!qTable.ContainsKey(stateHash))
                InitializeState(stateHash, GetValidMovesFromState(state));

            double nextMaxQ = 0;
            if (!IsTerminalState(nextState))
            {
                string nextStateHash = GetStateHash(nextState, GetNextPlayer(player));
                if (qTable.ContainsKey(nextStateHash))
                    nextMaxQ = qTable[nextStateHash].Values.DefaultIfEmpty().Max();
            }

            double oldQ = qTable[stateHash].ContainsKey(move) ? qTable[stateHash][move] : 0;
            double newQ = (1 - learningRate) * oldQ + learningRate * (reward + discountFactor * nextMaxQ);
            qTable[stateHash][move] = newQ;
        }

        private bool IsTerminalState(Player[] board)
        {
            var tempGame = new TicTacToeGame();
            for (int i = 0; i < 9; i++)
                if (board[i] != Player.None)
                    tempGame.MakeMove(i);

            return tempGame.GameOver;
        }

        private int[] GetValidMovesFromState(Player[] board)
        {
            return board.Select((p, i) => (p, i))
                        .Where(x => x.p == Player.None)
                        .Select(x => x.i)
                        .ToArray();
        }

        private Player GetNextPlayer(Player currentPlayer)
        {
            return currentPlayer == Player.X ? Player.O : Player.X;
        }

        private string GetStateHash(Player[] board, Player currentPlayer)
        {
            return string.Join("", board.Select(p => p == Player.X ? "X" : p == Player.O ? "O" : "-")) + "-" + currentPlayer;
        }

        public void SaveModel(string path)
        {
            var lines = new List<string>();
            foreach (var kvp in qTable)
            {
                var values = string.Join(",", kvp.Value.Select(kv => $"{kv.Key}:{kv.Value.ToString(CultureInfo.InvariantCulture)}"));
                lines.Add($"{kvp.Key}|{values}");
            }
            File.WriteAllLines(path, lines);
            File.WriteAllText(path + ".explore", explorationRate.ToString(CultureInfo.InvariantCulture));
        }

        public void LoadModel(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"File {path} not found");

            qTable = new Dictionary<string, Dictionary<int, double>>();

            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Split('|');
                if (parts.Length != 2) continue;

                var state = parts[0];
                var valuePairs = parts[1].Split(',');

                var values = new Dictionary<int, double>();
                foreach (var pair in valuePairs)
                {
                    var kv = pair.Split(':');
                    if (kv.Length != 2) continue;

                    if (int.TryParse(kv[0], out int move) &&
                        double.TryParse(kv[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                    {
                        values[move] = value;
                    }
                }

                qTable[state] = values;
            }

            // Load exploration rate
            var exploreFile = path + ".explore";
            if (File.Exists(exploreFile))
            {
                var exploreText = File.ReadAllText(exploreFile);
                if (double.TryParse(exploreText, NumberStyles.Any, CultureInfo.InvariantCulture, out double savedExplore))
                {
                    explorationRate = savedExplore;
                }
            }
        }

        public void DecreaseExploration(double decay = 0.9995, double minExploration = 0.001)
        {
            explorationRate = Math.Max(minExploration, explorationRate * decay);
        }

        public int GetStateCount() => qTable.Count;
    }

    class Program
    {
        static void Main(string[] args)
        {
            AnsiConsole.Write(new FigletText("QL Tic-Tac-Toe").Centered().Color(Color.Green));
            AnsiConsole.Write(new Rule("[yellow]Reinforcement Learning[/]").Centered());

            var agentX = new QLearningAgent();
            var agentO = new QLearningAgent();
            var game = new TicTacToeGame();

            bool trainingMode = AnsiConsole.Confirm("Enable AI training mode?");
            int trainingGames = trainingMode ? AnsiConsole.Ask<int>("How many training games?", 10000) : 0;
            double winReward = 1.0;
            double drawReward = 0.5;
            double lossReward = -1.0;

            // Train AI
            if (trainingMode && trainingGames > 0)
            {
                int reportInterval = Math.Max(1, trainingGames / 10);
                double winRateX = 0;
                double winRateO = 0;
                double drawRate = 0;

                AnsiConsole.Progress()
                    .AutoClear(false)
                    .Columns(new ProgressColumn[]
                    {
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn()
                        {
                            CompletedStyle = new Style(foreground: Color.Lime),
                            RemainingStyle = new Style(foreground: Color.Grey)
                        },
                        new PercentageColumn(),
                        new RemainingTimeColumn(),
                        new SpinnerColumn(Spinner.Known.Star)
                    })
                    .Start(ctx =>
                    {
                        var trainingTask = ctx.AddTask("Training AI", maxValue: trainingGames);
                        var winRateTask = ctx.AddTask("X Win Rate", maxValue: 100);
                        var lossRateTask = ctx.AddTask("O Win Rate", maxValue: 100);
                        var drawRateTask = ctx.AddTask("Draw Rate", maxValue: 100);

                        int winsX = 0;
                        int winsO = 0;
                        int draws = 0;

                        for (int i = 0; i < trainingGames; i++)
                        {
                            game.Reset();
                            var moveHistory = new List<(Player player, Player[] state, int move, Player[] nextState)>();

                            agentX.DecreaseExploration();
                            agentO.DecreaseExploration();

                            while (!game.GameOver)
                            {
                                Player currentPlayer = game.CurrentPlayer;
                                Player[] currentState = game.GetBoardState();

                                int move = (currentPlayer == Player.X) ?
                                    agentX.GetMove(game) :
                                    agentO.GetMove(game);

                                Player[] stateBeforeMove = game.GetBoardState();
                                game.MakeMove(move);
                                Player[] stateAfterMove = game.GetBoardState();

                                moveHistory.Add((currentPlayer, stateBeforeMove, move, stateAfterMove));
                            }

                            // Update Q-values
                            foreach (var (player, state, move, nextState) in moveHistory)
                            {
                                double reward;
                                if (game.Winner == player)
                                    reward = winReward;
                                else if (game.Winner == Player.None)
                                    reward = drawReward;
                                else
                                    reward = lossReward;

                                if (player == Player.X)
                                    agentX.UpdateQValue(state, player, move, reward, nextState);
                                else
                                    agentO.UpdateQValue(state, player, move, reward, nextState);
                            }

                            // Update statistics
                            if (game.Winner == Player.X) winsX++;
                            else if (game.Winner == Player.O) winsO++;
                            else draws++;

                            winRateX = (double)winsX / (i + 1) * 100;
                            winRateO = (double)winsO / (i + 1) * 100;
                            drawRate = (double)draws / (i + 1) * 100;

                            trainingTask.Increment(1);
                            winRateTask.Value = (int)winRateX;
                            lossRateTask.Value = (int)winRateO;
                            drawRateTask.Value = (int)drawRate;

                            if (i % reportInterval == 0 || i == trainingGames - 1)
                            {
                                AnsiConsole.MarkupLine($"\n[bold underline]Training Progress:[/]");
                                var statsTable = new Table()
                                    .AddColumn("Metric")
                                    .AddColumn("Value", c => c.RightAligned())
                                    .AddRow("[green]X Wins[/]", $"{winRateX:F1}%")
                                    .AddRow("[blue]O Wins[/]", $"{winRateO:F1}%")
                                    .AddRow("[yellow]Draws[/]", $"{drawRate:F1}%")
                                    .AddRow("[grey]Exploration Rate[/]", $"{agentX.ExplorationRate:P2}")
                                    .Border(TableBorder.Rounded)
                                    .BorderStyle(new Style(Color.Grey));

                                AnsiConsole.Write(statsTable);
                            }
                        }
                    });

                agentX.SaveModel("qtable_x.txt");
                agentO.SaveModel("qtable_o.txt");
                AnsiConsole.MarkupLine($"[bold green]✓ Training completed! Models saved.[/]");
                AnsiConsole.MarkupLine($"[grey]Final exploration rate: {agentX.ExplorationRate:P2}[/]");
            }
            else if (!trainingMode)
            {
                bool modelXLoaded = false;
                bool modelOLoaded = false;

                try
                {
                    agentX.LoadModel("qtable_x.txt");
                    modelXLoaded = true;
                    AnsiConsole.MarkupLine($"[bold green]✓ X Model loaded! States: {agentX.GetStateCount()}, Explore: {agentX.ExplorationRate:P2}[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[bold red]⚠ X Error: {ex.Message}[/]");
                }

                try
                {
                    agentO.LoadModel("qtable_o.txt");
                    modelOLoaded = true;
                    AnsiConsole.MarkupLine($"[bold green]✓ O Model loaded! States: {agentO.GetStateCount()}, Explore: {agentO.ExplorationRate:P2}[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[bold red]⚠ O Error: {ex.Message}[/]");
                }

                if (!modelXLoaded || !modelOLoaded)
                {
                    AnsiConsole.MarkupLine("[bold yellow]⚠ Using partially trained agents[/]");
                }

                // Imposta tasso di esplorazione molto basso per il gioco
                agentX.SetExplorationRate(0.001);
                agentO.SetExplorationRate(0.001);
                AnsiConsole.MarkupLine($"[grey]Exploration set to: X={agentX.ExplorationRate:P2}, O={agentO.ExplorationRate:P2}[/]");
            }

            // Game loop
            bool playAgain = true;
            while (playAgain)
            {
                game.Reset();
                Player humanPlayer = AnsiConsole.Prompt(
                    new SelectionPrompt<Player>()
                        .Title("Choose your side")
                        .PageSize(3)
                        .AddChoices(Player.X, Player.O)
                        .UseConverter(p => p == Player.X ?
                            "[red]X[/]" :
                            "[blue]O[/]")
                );

                while (!game.GameOver)
                {
                    AnsiConsole.Clear();
                    AnsiConsole.Write(new Rule($"[yellow]Turn: {(game.CurrentPlayer == humanPlayer ? "Your" : "AI's")}[/]"));
                    DisplayBoard(game.GetBoardState());
                    AnsiConsole.WriteLine();

                    if (game.CurrentPlayer == humanPlayer)
                    {
                        var prompt = new TextPrompt<int>("Enter position [green](0-8)[/]:")
                            .PromptStyle("yellow")
                            .ValidationErrorMessage("[red]Invalid position![/]")
                            .Validate(m =>
                            {
                                if (m < 0 || m > 8) return false;
                                return game.GetBoardState()[m] == Player.None;
                            });

                        int move = AnsiConsole.Prompt(prompt);
                        game.MakeMove(move);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]🤖 AI is thinking...[/]");
                        Task.Delay(500).Wait();

                        int move = (game.CurrentPlayer == Player.X) ?
                            agentX.GetMove(game) :
                            agentO.GetMove(game);

                        game.MakeMove(move);
                    }
                }

                AnsiConsole.Clear();
                AnsiConsole.Write(new Rule("[bold]GAME OVER[/]"));
                DisplayBoard(game.GetBoardState());
                AnsiConsole.WriteLine();

                if (game.Winner != Player.None)
                {
                    var winnerPanel = new Panel($"[bold]{game.Winner}[/]")
                        .Header("🏆 WINNER")
                        .BorderColor(game.Winner == Player.X ? Color.Red : Color.Blue)
                        .Border(BoxBorder.Double);

                    AnsiConsole.Write(winnerPanel);
                }
                else
                {
                    AnsiConsole.Write(new Panel("[bold yellow]🤝 DRAW![/]")
                        .BorderColor(Color.Yellow)
                        .Border(BoxBorder.Double));
                }

                playAgain = AnsiConsole.Confirm("[grey]Play again?[/]");
            }

            AnsiConsole.MarkupLine("[bold green]Thanks for playing![/]");
        }

        static void DisplayBoard(Player[] board)
        {
            var table = new Table()
                .Centered()
                .Border(TableBorder.Square)
                .BorderColor(Color.Grey)
                .AddColumns(
                    new TableColumn("").Centered(),
                    new TableColumn("").Centered(),
                    new TableColumn("").Centered()
                );

            for (int row = 0; row < 3; row++)
            {
                var rowCells = new string[3];
                for (int col = 0; col < 3; col++)
                {
                    int index = row * 3 + col;
                    rowCells[col] = board[index] switch
                    {
                        Player.X => "[bold red]X[/]",
                        Player.O => "[bold blue]O[/]",
                        _ => $"[dim]{index}[/]"
                    };
                }
                table.AddRow(rowCells[0], rowCells[1], rowCells[2]);
            }

            var panel = new Panel(table)
                .Border(BoxBorder.None)
                .Padding(1, 0, 1, 0);

            AnsiConsole.Write(panel);
        }
    }
}