import XCTest
import SwiftTreeSitter
import TreeSitterTreeSitterLimelightx

final class TreeSitterTreeSitterLimelightxTests: XCTestCase {
    func testCanLoadGrammar() throws {
        let parser = Parser()
        let language = Language(language: tree_sitter_tree_sitter_limelightx())
        XCTAssertNoThrow(try parser.setLanguage(language),
                         "Error loading TreeSitterLimelightx grammar")
    }
}
