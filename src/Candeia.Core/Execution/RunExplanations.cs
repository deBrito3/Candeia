namespace Candeia.Execution;

// What the player reads when a test fails with an exception or a run stops early, in both languages.
public static class RunExplanations
{
    public static (string Pt, string En) Error(RuntimeError error)
    {
        string type = error.Type.Split('.').Last();
        return error.Type switch
        {
            "System.DivideByZeroException" => ("divisão por zero: o código dividiu um número inteiro por 0.",
                "division by zero: the code divided a whole number by 0."),
            "System.IndexOutOfRangeException" => ("posição fora do array: o índice usado não existe. Os índices vão de 0 até Length - 1.",
                "position outside the array: that index does not exist. Indexes go from 0 to Length - 1."),
            "System.ArgumentOutOfRangeException" => ("posição fora do limite: o índice ou o tamanho usado não existe nessa lista ou nesse texto.",
                "position out of range: that index or length does not exist in this list or text."),
            "System.NullReferenceException" => ("o código usou algo que vale `null` como se fosse um objeto de verdade.",
                "the code used something that is `null` as if it were a real object."),
            "System.FormatException" => ("o texto não está num formato válido, por exemplo `int.Parse` de algo que não é número.",
                "the text is not in a valid format, for example `int.Parse` of something that is not a number."),
            "System.OverflowException" => ("o número ficou grande demais para o seu tipo.", "the number got too big for its type."),
            "System.Collections.Generic.KeyNotFoundException" => ("essa chave não existe no dicionário. Confira com `ContainsKey` ou use `TryGetValue`.",
                "that key is not in the dictionary. Check with `ContainsKey` or use `TryGetValue`."),
            "System.InvalidCastException" => ("conversão de tipo inválida.", "invalid type conversion."),
            "System.OutOfMemoryException" => ("o código usou memória demais.", "the code used too much memory."),
            _ => ($"o seu código lançou `{type}`: {error.Message}", $"your code threw `{type}`: {error.Message}"),
        };
    }

    public static (string Pt, string En)? Stop(RunResult result, long stepLimit) => result.Stop switch
    {
        RunStop.Completed => null,
        RunStop.StepLimit => ($"o código passou de {stepLimit:N0} passos sem terminar. Provavelmente um laço nunca acaba: confira a condição do `while` ou do `for`.",
            $"the code went past {stepLimit:N0} steps without finishing. Probably a loop never ends: check the `while` or `for` condition."),
        RunStop.Timeout => ("o código demorou demais e foi interrompido.", "the code took too long and was stopped."),
        RunStop.StackOverflow => ("o código chamou a si mesmo sem parar (recursão infinita). Todo método que chama a si mesmo precisa de um caso que pare.",
            "the code called itself without stopping (infinite recursion). Every method that calls itself needs a case that stops."),
        RunStop.OutOfMemory => ("o código usou memória demais e foi interrompido.", "the code used too much memory and was stopped."),
        RunStop.ProcessExit => ("o código encerrou o programa antes do fim dos testes.", "the code ended the program before the tests finished."),
        RunStop.EntryNotFound => ($"o sistema da casa procura `{result.Detail}`, mas não encontrou. Confira o nome da classe, do método e os parâmetros.",
            $"the house system looks for `{result.Detail}` but could not find it. Check the class name, the method name and the parameters."),
        _ => ($"o executor do sistema falhou: {result.Detail}", $"the system's runner failed: {result.Detail}"),
    };
}
