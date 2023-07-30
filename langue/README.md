![Github](https://img.shields.io/github/tag/Pannoniae/EconomyBot.svg)


# Langue

noun Linguistics
noun: langue; plural noun: langues

    a language viewed as an abstract system used by a speech community, in contrast to the actual linguistic behaviour of individuals.

This is a language analysis and language manipulation subsystem of EconomyBot.

# Typical workflow

> This project uses [Poetry](https://python-poetry.org), ensure you have _Poetry_ installed

```sh
$ pip3 install poetry

$ poetry --version
Poetry version 1.1.12

```


To create the _virtual env_ with all needed dependencies, run this in the `py` folder:

```sh
$ poetry install
$ poetry shell
(.venv) $ langue --help
```

## Run the tests

To run the tests

```sh
# to run the tests:
$ poetry run pytest
# to get the coverage
$ poetry run pytest
```

## Build your app

You can use _Poetry_ to build your app and get a `.whl`

```sh
$ poetry build
Building langue (0.1.0)
  - Building sdist
  - Built langue-0.1.0.tar.gz
  - Building wheel
  - Built langue-0.1.0-py3-none-any.whl

$ ls dist
langue-0.1.0-py3-none-any.whl  langue-0.1.0.tar.gz
```

## Publish your app

You can use _Poetry_ to publish your app to [PyPI](https://pypi.org)

```sh
$ poetry publish
```

By default, _Github Actions_ are configured to

- build your app and run your unit tests with coverage on every push
- build the wheel package and upload it to Pypi on every tag

> Note: to allow Github CI to publish on PyPI, you need to [create a token](https://pypi.org/manage/account/token/) and add it to [your project settings](https://github.com/essembeh/python-Langue/settings/secrets/actions), the name of the token should be `PYPI_TOKEN`

I personnally use this command to bump the version using poetry, create the associated git tag and push to Github:

```sh
# for a patch bump
$ poetry version patch && git commit -a -m '🔖 New release' && git tag -f $(poetry version -s) && git push --tags
# for a minor bump
$ poetry version minor && git commit -a -m '🔖 New release' && git tag -f $(poetry version -s) && git push --tags
# for a major bump
$ poetry version major && git commit -a -m '🔖 New release' && git tag -f $(poetry version -s) && git push --tags
```


# Tools

[Poetry](https://python-poetry.org), a very useful tool to avoid boilerplate code (`setup.py`, `requirements.txt`, `requirements-dev.txt` ...)

[Pylint](https://www.pylint.org/): to report many warnings/errors (and VSCodium is configured to show them).

[Black](https://pypi.org/project/black/): a strict code formatter that automatically format source code on save using _VSCodium_.

[Gitmoji](https://gitmoji.dev/): a list of emoji to get a more efficient history
