cm-gun-unskilled = Ви не знаєте як використовувати {$gun}
cm-gun-no-ammo-message = Скінчилися набої!
cm-gun-use-delay = Зачекайте {$seconds} секунд перед тим як знову вистрілити!
cm-gun-pump-examine = [bold]Натисніть вашу [color=cyan]унікальну[/color] клавішу (Spacebar за замовчуванням) щоб прокачати перед пострілом.[/bold]
cm-gun-pump-first-with = Вам спершу потрібно прокачати зброю за допомогою {$key}!
cm-gun-pump-first = Спочатку потрібно прокачати зброю!

rmc-breech-loaded-open-shoot-attempt = Спершу потрібно закрити затвор!
rmc-breech-loaded-not-ready-to-shoot = Спершу треба передьорнуть затвор!
rmc-breech-loaded-closed-load-attempt = Спершу треба відкрити затвор!
rmc-breech-loaded-closed-extract-attempt = Спершу відкрийте затвор!
rmc-breech-loaded-toggle-attempt-cooldown = Ви маєте почекати щоб знову {$action} затвор!
rmc-breech-loaded-open = відкрити
rmc-breech-loaded-close = закрити

rmc-wield-use-delay = Спочатку зачекайте {$seconds} секунд перш ніж використовувати {$wieldable}!
rmc-shoot-use-delay = Спочатку зачекайте {$seconds} секунд перед тим як стріляти з {$wieldable}!

rmc-shoot-harness-required = Необхідне спорядження
rmc-wear-smart-gun-required = Вам треба мати СмартГан, щоб носити їх.
rmc-gun-arc-blocked = Ви не можете стріляти поза межами дуги вогню зброї.

rmc-shoot-id-lock-unauthorized = Гачок заблоковано. НЕАВТОРИЗОВАНИЙ КОРИСТУВАЧ.
rmc-id-lock-unauthorized = ВІДХИЛЕНО. НЕАВТОРИЗОВАНИЙ КОРИСТУВАЧ.
rmc-id-lock-authorization = Ви взяли {$gun}, авторизуючись як власник.
rmc-id-lock-authorization-combat = {$gun} пікає, авторизуючи вас як власника.
rmc-id-lock-toggle-lock = Ви {$action} індентифікаціний замок на {$gun}.

rmc-id-lock-color-unauthorized = червоним
rmc-id-lock-color-authorized = шартрезовим
rmc-id-lock-toggle-on = заблокували
rmc-id-lock-toggle-off = розблокували

rmc-iff-toggle = Ви {$action} УДВ для {$gun}.
rmc-iff-toggle-off = вимкнули
rmc-iff-toggle-on = увімкнули

rmc-revolver-spin = Ви крутите барабан.

rmc-examine-text-weapon-accuracy = Поточний множник точності [color={$colour}]{TOSTRING($accuracy, "F2")}[/color].

rmc-examine-text-scatter-max = Поточний максимальний розкид [color={$colour}]{TOSTRING($scatter, "F1")}[/color] градусів.
rmc-examine-text-scatter-min = Поточний мінімальний розкид [color={$colour}]{TOSTRING($scatter, "F1")}[/color] градусів.
rmc-examine-text-shots-to-max-scatter = Треба ще [color={$colour}]{$shots}[/color] пострілів до максимального розкиду.
rmc-examine-text-iff = [color=cyan]Ця зброя стріляє повз союзників, ігноруючи їх![/color]
rmc-examine-text-id-lock-no-user = [color=chartreuse]Незареєстровано. Візьміть в руки щоб зареєструватись як власник.[/color]
rmc-examine-text-id-lock = [color=chartreuse]Зареєстровано на [/color][color={$color}]{$name}[/color][color=chartreuse].[/color]
rmc-examine-text-id-lock-unlocked = [color=chartreuse]Зареєстровано на [/color][color={$color}]{$name}[/color][color=chartreuse], але має розблоковану функцію стрільби.[/color]
rmc-examine-text-execute = [color=red]Ця зброя може використовуватись для страти за наявності необхідної навички![/color]

rmc-gun-rack-examine = [bold]Натисніть свою [color=cyan]унікальну[/color] клавішу (Пробіл за замовченням) щоб поставити перед стрільбою.[/bold]
rmc-gun-rack-first-with = Вам спочатку потрібно поставити зброю за допомогою {$key}!
rmc-gun-rack-first = Спочатку треба поставити зброю!

rmc-assisted-reload-fail-angle = Ви маєте стояти позаду {$target} що перезарядити зброю {$target}!
rmc-assisted-reload-fail-full = {$weapon} {CAPITALIZE($target)} вже заряджено.
rmc-assisted-reload-fail-mismatch = {$ammo} не можна зарядити в {$weapon}!
rmc-assisted-reload-start-user = Ви починаєте перезаряджати {$weapon} {$target}! Не ворушіться...
rmc-assisted-reload-start-target = {$reloader} починає перезаряджати вашу {$weapon} з {$ammo}! Не ворушіться...

rmc-gun-stacks-hit-single = Вцілив!
rmc-gun-stacks-hit-multiple = Вцілив! {$hits} влучань поспіль!
rmc-gun-stacks-reset = {$weapon} втрачає дані про ціль, і перемикається на стандарний режим стрільби.

rmc-gun-shoot-air-self = ВИ СТРІЛЯЄТЕ З { CAPITALIZE($weapon) } У ПОВІТРЯ!
rmc-gun-shoot-air-other = { CAPITALIZE($user) } СТРІЛЯЄ { CAPITALIZE($weapon) } В ПОВІТРЯ!
rmc-gun-shoot-air-blocked = Дах над вами занадто щільний.
rmc-gun-shoot-air-examine = [bold]Натисність вашу [color=cyan]унікальну[/color] клавішу (Spacebar за замовчуванням){$harm ->
    [true] {" доки в бойовому режимі"}
    *[false] {""}
    } щоб вистрілити в повітря.[/bold]

rmc-flare-gun-examine = Останній вистрілений сигнальний фаєр має розташування: [color=#ad3b98][bold]{$id}[/bold][/color]

expendable-light-starshell-ash-empty-name = перегорівший попіл зіркового снаряду
expendable-light-starshell-ash-empty-desc = Вигорівші залишки зіркового снаряду
