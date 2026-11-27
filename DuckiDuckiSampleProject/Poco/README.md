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

## Generic-база и статические свойства интерфейса

Если наследник передаёт себя первым параметром в generic-базу, задайте эту
базу на элементе перечисления. Генератор автоматически подставит имя класса
первым аргументом, а остальные типы возьмёт из `PocoBase`. Выбранная база
должна наследовать базовый класс семейства.

```csharp
[PocoFamily(typeof(AutoStage), Sealed = true)]
[PocoInterface(typeof(IAutoStage))]
internal enum MeasurementStages
{
    [PocoBase(typeof(AutoStage<,>), typeof(PkiMeasurementStageParameters))]
    [PocoValue("caption", "Съём данных с ПКИ")]
    [PocoValue("label", "Съём данных с ПКИ")]
    [PocoValue("contract", "pki_measurement_stage")]
    PkiMeasurementStage
}

public interface IAutoStage
{
    [PocoVariable("caption")]
    static abstract string StageCaption { get; }
}
```

Добавьте отдельный `[PocoInterface(typeof(...))]` для каждого интерфейса семейства.
Для генерации статического абстрактного свойства только для чтения пометьте
его атрибутом `PocoVariable` в интерфейсе. Члены интерфейса можно также реализовать в
ручной части класса. `Sealed = true` добавляет `sealed` конкретным классам
семейства. Для статических абстрактных членов интерфейса нужен C# 11 и
целевая платформа с их поддержкой, например .NET 7 или новее.

В `GenericStageFamily.cs` показаны все четыре этапа, оба варианта generic-базы
и перенос контракта через `PocoPrototype`.

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
