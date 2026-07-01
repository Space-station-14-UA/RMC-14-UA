rmc-medical-examine-unrevivable = [color=purple][italic]{CAPITALIZE(POSS-ADJ($victim))} очі пусті, жодних ознак життя.[/italic][/color]

rmc-medical-examine-headless = [color=purple][italic]{CAPITALIZE(SUBJECT($victim))} {CONJUGATE-BE($victim)} безумовно мертвий.[/italic][/color]

rmc-medical-examine-unconscious = [color=lightblue]{ CAPITALIZE(SUBJECT($victim)) } { GENDER($victim) ->
    [epicene] здається
    *[other] здаються
  } без тями.[/color]

rmc-medical-examine-dead = [color=red]{CAPITALIZE(SUBJECT($victim))} {CONJUGATE-BE($victim)} не дихає.[/color]

rmc-medical-examine-dead-simple-mob = [color=red]{CAPITALIZE(SUBJECT($victim))} {CONJUGATE-BE($victim)} МРЕЦЬ. Відкинув копита.[/color]

rmc-medical-examine-dead-xeno = [color=red]{CAPITALIZE(SUBJECT($victim))} {CONJUGATE-BE($victim)} МРЕЦЬ. Відкинула хвоста. Аж до самісінього вулику в небі.[/color]

rmc-medical-examine-alive = [color=green]{CAPITALIZE(SUBJECT($victim))} {CONJUGATE-BE($victim)} живий та дихає.[/color]

rmc-medical-examine-bleeding = [color=#d10a0a]{CAPITALIZE(SUBJECT($victim))} {CONJUGATE-HAVE($victim)} кровоточучі рани на {POSS-ADJ($victim)} тілі.[/color]

rmc-medical-examine-verb = Показати медичні дії
