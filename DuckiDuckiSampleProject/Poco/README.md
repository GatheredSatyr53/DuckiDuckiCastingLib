# Генерация семейств POCO

За генерацию POCO отвечает `PocoGenerator`. Roslyn загружает его и `DuckCastGenerator` из одной сборки анализатора `DuckiDuckiCastingLib`.

## Подключение

Скачайте архив `DuckiDuckiCastingLib-nuget` из GitHub Actions, распакуйте его и добавьте папку с `.nupkg` как локальный источник NuGet. Подключите пакет `DuckiDuckiCastingLib` с версией из имени файла. Для пакета нужен компилятор Roslyn 4.14 или новее, например из .NET SDK 10.

```xml
<ItemGroup>
  <PackageReference Include="DuckiDuckiCastingLib" Version="0.1.0-ci.RUN_ID.RUN_ATTEMPT" PrivateAssets="all" />
</ItemGroup>
```

Если вы подключаете генераторы из исходников, используйте ссылку на проект анализатора.

```xml
<ItemGroup>
  <ProjectReference Include="../DuckiDuckiCastingLib/DuckiDuckiCastingLib.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Семейство классов по перечислению

Добавьте `[PocoFamily(typeof(БазовыйКласс))]` на перечисление. Имена его элементов
станут именами публичных `partial`-классов в пространстве имён перечисления.
Пометьте виртуальные или абстрактные свойства с одним `get` и методы без параметров
атрибутом `[PocoVariable("имя")]`. Передайте значения для конкретного класса атрибутами
`[PocoValue("имя", значение)]` на соответствующем элементе перечисления.

```csharp
[PocoFamily(typeof(MyStage))]
internal enum StageFamily
{
    [PocoValue("caption", "Термоцикл")]
    ThermalStage,

    [PocoValue("caption", "Горение")]
    CombustionStage
}

public abstract class MyStage
{
    [PocoVariable("caption")]
    public abstract string Caption { get; }
}
```

Для семейства достаточно ссылки на анализатор. Значения можно использовать также
в строковых шаблонах и связанных константах прототипа базового класса. Пропущенное
значение свойства или метода оставляет этот член за базовым или ручным `partial`-классом.
Если остаётся абстрактный член, сгенерированный класс будет абстрактным. Переменные,
используемые прототипом, обязательны. В `AutoStageFamily.cs` есть пример для свойств,
методов и прототипа. Ошибки значений выдаются как `POCO001` на элементе перечисления.

## Константы прототипа

Генератор переносит поля `const`, объявленные в прототипе, сохраняя имя, тип и доступность.
Обычное значение берётся из C#. В строковой константе можно использовать шаблон `$name` или `${name}`.

Укажите тип прототипа на базовом классе через `[PocoPrototype(typeof(AutoStageProto))]`.
Чтобы взять значение константы из `PocoValue`, укажите имя переменной в `PocoVariable`:

```csharp
public const string Contract = "$stage_contract";

[PocoVariable("retry_limit")]
public const int RetryLimit = 0;
```

На элементе перечисления укажите `[PocoValue("stage_contract", "thermal_cycle")]`
и `[PocoValue("retry_limit", 3)]`. Значение для связанной константы должно быть
совместимо с её типом. Строковые шаблоны в константах и атрибутах прототипа
принимают строковые значения.

## Запуск примера

```sh
dotnet run --project DuckiDuckiSampleProject
```

Пример проверяет свойства, методы, подстановку контракта, неполную спецификацию и ручной `partial`. Атрибуты fastJSON в примере имитируют интерфейс атрибутов TTS. Полиморфную сериализацию и десериализацию нужно проверять в проекте TTS с реальным `FastJsonHelper.CreatePolymorphicParameters`.
