"""
analysis engine
"""
import inspect


def is_transformation(transformation) -> bool:

    """
    a valid transformation is a callable that has a string input and a string output
    """

    result = True
    if inspect.isfunction(transformation):
        annotations = transformation.__annotations__
        try:
            if annotations["return"] != str:
                result = False
        except KeyError:
            result = False
        try:
            input_type = annotations["input_text"]
            if input_type != str:
                result = False
        except KeyError:
            result = False
    else:
        result = False

    # TODO allow callable classes, too
    return result


class Engine:
    """
    Performs grammatical analysis and transformations
    """

    def __init__(self):
        self._transformations = []

    def register_transformation(self, transformation, *args, **kwargs):
        if is_transformation(transformation):
            self._transformations.append((transformation, args, kwargs))
        else:
            raise TypeError("Wrong transformation")

    def __call__(self, input_text: str) -> str:
        result = input_text
        for transformation, args, kwargs in self._transformations:
            result = transformation(result, *args, **kwargs)
        return result
