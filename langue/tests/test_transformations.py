import pytest
from engine import Engine

from transformations import replace_word


def do_nothing(input_text: str) -> str:
    return input_text


def test_identity_transformation():
    engine = Engine()
    engine.register_transformation(do_nothing)
    engine.register_transformation(do_nothing)
    assert engine("This is a lovely day") == "This is a lovely day"


def test_replace_transformation():
    engine = Engine()
    engine.register_transformation(do_nothing)
    engine.register_transformation(replace_word, "day", "night")
    engine.register_transformation(replace_word, "banana", "mango")
    assert engine("This is a lovely day") == "This is a lovely night"
    assert engine("I had a banana yesterday") == "I had a mango yesterday"


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
