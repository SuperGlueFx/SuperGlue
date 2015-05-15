﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fclp;

namespace SuperGlue
{
    class Program
    {
        private static readonly IDictionary<string, Func<FluentCommandLineParser, string[], ICommand>> CommandBuilders = new Dictionary<string, Func<FluentCommandLineParser, string[], ICommand>>
        {
            {"run", BuildRunCommand},
            {"add", BuildAddProjectFromTemplateCommand}
        };

        static void Main(string[] args)
        {
            var parser = new FluentCommandLineParser();

            var commandName = args.FirstOrDefault() ?? "";

            if (!CommandBuilders.ContainsKey(commandName))
            {
                Console.WriteLine("{0} isn't a valid command. Available commands: {1}.", commandName, string.Join(", ", CommandBuilders.Select(x => x.Key)));
                return;
            }

            var commandArgs = args.Skip(1).ToArray();

            var command = CommandBuilders[commandName](parser, commandArgs);

            command.Execute();
        }

        private static RunCommand BuildRunCommand(FluentCommandLineParser parser, string[] args)
        {
            var command = new RunCommand();

            parser
                .Setup<string>('p', "path")
                .Callback(x => command.AppPath = Path.IsPathRooted(x) ? x : Path.Combine(Environment.CurrentDirectory, x))
                .SetDefault("");

            parser.Parse(args);

            return command;
        }

        private static AddProjectFromTemplateCommand BuildAddProjectFromTemplateCommand(FluentCommandLineParser parser, string[] args)
        {
            var command = new AddProjectFromTemplateCommand();

            parser
                .Setup<string>('n', "name")
                .Callback(x => command.Name = x)
                .Required();

            parser
                .Setup<string>('t', "template")
                .Callback(x => command.Template = x)
                .Required();

            parser
                .Setup<string>('s', "solution")
                .Callback(x => command.Solution = x)
                .Required();

            parser.Parse(args);

            return command;
        }
    }
}