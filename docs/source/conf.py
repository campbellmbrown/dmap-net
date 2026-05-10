# Configuration file for the Sphinx documentation builder.
#
# For the full list of built-in configuration values, see the documentation:
# https://www.sphinx-doc.org/en/master/usage/configuration.html

# -- Project information -----------------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#project-information

project = 'DMap'
copyright = '2026, Campbell Brown'
author = 'Campbell Brown'

# -- General configuration ---------------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#general-configuration

extensions = [
    'sphinx.ext.extlinks',
]

extlinks = {
    'release-link': ('https://github.com/campbellmbrown/dmap-net/releases/tag/%s', '%s'),
}

templates_path = ['_templates']
exclude_patterns = []

# -- Options for HTML output -------------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#options-for-html-output

html_theme = 'sphinx_rtd_theme'
html_context = {
    "display_github": True,
    "github_user": "campbellmbrown",
    "github_repo": "dmap-net",
    "github_version": "main",
    "conf_py_path": "/docs/source/",
}
html_static_path = ['_static']
html_css_files = [
    'css/custom.css',
]
