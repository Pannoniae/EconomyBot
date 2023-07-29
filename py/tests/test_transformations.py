import pytest
from engine import Engine


def do_nothing(input_text: str) -> str:
    return input_text


def day_to_night(input_text: str) -> str:
    return input_text.replace("day", "night")


def test_identity_transformation():
    engine = Engine()
    engine.register_transformation(do_nothing)
    engine.register_transformation(do_nothing)
    assert engine("This is a lovely day") == "This is a lovely day"


def test_replace_transformation():
    engine = Engine()
    engine.register_transformation(do_nothing)
    engine.register_transformation(day_to_night)
    assert engine("This is a lovely day") == "This is a lovely night"


def test_wrong_transformation():

    engine = Engine()

    def bad_trafo1(bemenet: str, times: int):
        return

    def bad_trafo2(bemenet: str):
        return

    def bad_trafo3(input_text: list):
        return

    with pytest.raises(TypeError):
        engine.register_transformation(bad_trafo1)

    with pytest.raises(TypeError):
        engine.register_transformation(bad_trafo2)

    with pytest.raises(TypeError):
        engine.register_transformation(bad_trafo3)

    with pytest.raises(TypeError):
        engine.register_transformation("bad_transform")
