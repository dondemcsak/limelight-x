package tree_sitter_tree_sitter_limelightx_test

import (
	"testing"

	tree_sitter "github.com/tree-sitter/go-tree-sitter"
	tree_sitter_tree_sitter_limelightx "github.com/tree-sitter/tree-sitter-tree_sitter_limelightx/bindings/go"
)

func TestCanLoadGrammar(t *testing.T) {
	language := tree_sitter.NewLanguage(tree_sitter_tree_sitter_limelightx.Language())
	if language == nil {
		t.Errorf("Error loading TreeSitterLimelightx grammar")
	}
}
