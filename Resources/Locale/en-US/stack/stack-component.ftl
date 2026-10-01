### UI

# Shown when a stack is examined in details range
comp-stack-examine-detail-count = {$count ->
    [one] Це має [color={$markupCountColor}]{$count}[/color] штуку
    *[other] Це має [color={$markupCountColor}]{$count}[/color] штук
} в купці.

# Stack status control
comp-stack-status = Кількісь: [color=white]{$count}[/color]

### Interaction Messages

# Shown when attempting to add to a stack that is full
comp-stack-already-full = Купка вже повна.

# Shown when a stack becomes full
comp-stack-becomes-full = Тепер купка повна.

# Text related to splitting a stack
comp-stack-split = Ви розділили купку.
comp-stack-split-halve = Навпіл
comp-stack-split-too-small = Купка занадто мала, щоб її ділити.
