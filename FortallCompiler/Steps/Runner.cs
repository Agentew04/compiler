using System.Diagnostics;
using ELFSharp.ELF;
using SAAE.Engine.Mips.Runtime;
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

        Stopwatch sw = new();
        sw.Start();
        ulong clocks = 0;
        while (!machine.IsClockingFinished()) {
            machine.Clock();
            clocks++;
        }
        sw.Stop();

        Console.WriteLine("Valor de T0: " + machine.Registers.Get(RegisterFile.Register.T0));

        Console.WriteLine("-=-=- Estatisticas -=-=-");
        Console.WriteLine($"Duracao total: {sw.Elapsed}" );
        Console.WriteLine($"Clocks: {clocks}");
        Console.WriteLine($"Tempo medio por clock: {sw.Elapsed/clocks}");
    }
}