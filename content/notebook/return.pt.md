# return: a resposta que a casa recebe

Cada método é uma pergunta que a casa faz. O `return` é a resposta que ela recebe.

```csharp
public static int Watts()
{
    int watts = 60;
    return watts;
}
```

A palavra antes do nome (`int`) diz o tipo da resposta. O `return` precisa entregar um valor desse tipo, e o método termina ali: o que vier depois do `return` não roda.

`public static` quer dizer que o sistema da casa pode chamar o método sem criar nada antes. A `class` em volta junta os métodos de uma mesma peça da casa. Um método `void` não devolve nada: só faz alguma coisa.
