﻿using System.Diagnostics;
using ELFSharp.ELF;
using SAAE.Engine.Mips.Runtime;
using SAAE.Engine.Mips.Runtime.Simple;
using Machine = SAAE.Engine.Mips.Runtime.Machine;

namespace FortallCompiler.Steps;

public class Runner {

    public void Run(string path) {
        Stream stdin = Console.OpenStandardInput();
        Stream stdout = Console.OpenStandardOutput();
        Stream stderr = Console.OpenStandardError();
        Machine machine = new MachineBuilder()
            .With4GbRam()
            .WithMipsMonocycle()
            .WithMarsOs()
            .WithStdio(stdin, stdout, stderr)
            .Build();

        ELF<uint> elf = ELFReader.Load<uint>(path);
        
        machine.LoadElf(elf);
        machine.Registers[RegisterFile.Register.Pc] = 0x0040_0000;
        machine.Cpu.OnSignalException += (sender, args) =>
        {
            if (args.Signal == Monocycle.SignalExceptionEventArgs.SignalType.Breakpoint)
            {
                Console.WriteLine("BREAKPOINT HIT");
                if (!Debugger.IsAttached)
                {
                    Debugger.Launch();
                }
                Debugger.Break();
            }
        };

        Console.WriteLine("-=-=- MAQUINA -=-=-");
        Console.WriteLine("CPU: " + machine.Cpu.GetType().Name);
        Console.WriteLine("OS: " + machine.Os.FriendlyName);
        Console.WriteLine("Architecture: " + machine.Os.CompatibleArchitecture);
        Console.WriteLine("Memory Size: " + machine.Memory.Size/ (1024 * 1024) + "MB");
        Console.WriteLine("Memory Type: " + machine.Memory.GetType().Name);
        Console.WriteLine("-=-=- EXECUCAO -=-=-");

        Stopwatch sw = new();
        sw.Start();
        ulong clocks = 0;
        while (!machine.IsClockingFinished()) {
            machine.Clock();
            clocks++;
        }
        sw.Stop();

        Console.WriteLine("-=-=- FIM EXECUCAO -=-=-");
        Console.WriteLine("Valor de saida: " + machine.Cpu.ExitCode);
        double frequency = clocks / sw.Elapsed.TotalSeconds;
        string frequencyUnit = "Hz";
        if (frequency > 1_000) {
            frequencyUnit = "kHz";
            frequency /= 1_000;
        }else if(frequency > 1_000_000) {
            frequencyUnit = "MHz";
            frequency /= 1_000_000;
        }else if (frequency > 1_000_000_000) {
            frequencyUnit = "GHz";
            frequency /= 1_000_000_000;
        }

        Console.WriteLine("-=-=- Estatisticas -=-=-");
        Console.WriteLine($"Duracao total: {sw.Elapsed}" );
        Console.WriteLine($"Clocks: {clocks}");
        Console.WriteLine($"Tempo medio por clock: {sw.Elapsed/clocks}");
        Console.WriteLine($"Frequencia: {frequency:F2}{frequencyUnit}");
    }
}