class TestClass
  def method_test(method_param)
  end

  def test_method()
  end
end

begin
    TestClass.new().test_method()
rescue Exception => err
    puts "Exception: #{err}"
end